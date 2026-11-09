import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  IRetryPolicy,
  LogLevel,
} from '@microsoft/signalr';
import { Observable, Subject, filter } from 'rxjs';

import { Auth } from '@core/auth/auth';
import { NotificationService } from '@core/notifications/notification.service';
import { HUB_PATH, NOTIFY_METHOD, RealtimeStatus, ServerNotification } from './realtime.model';

/** Паузы перед попытками переподключения: часто в начале, редко потом. */
const RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];

/** Сколько идентификаторов помнить, чтобы не показать одно сообщение дважды. */
const SEEN_LIMIT = 200;

/**
 * Связь с сервером в реальном времени.
 *
 * Подключение поднимается один раз на вкладку и живёт, пока открыт портал. Экраны на него
 * не подписываются напрямую — они берут <see cref="onEntity"/> для своей сущности и
 * перечитывают данные обычным запросом.
 *
 * Важно: хаб открывается только после прогрева доменной аутентификации (см. provide-realtime.ts).
 * SignalR ходит мимо HttpClient, а значит мимо NTLM-гейта, и подключение до прогрева запустило бы
 * ещё один Negotiate-handshake параллельно с остальными запросами.
 */
@Injectable({ providedIn: 'root' })
export class Realtime {
  private readonly auth = inject(Auth);
  private readonly notifications = inject(NotificationService);

  private connection?: HubConnection;
  private readonly seen = new Set<string>();
  private readonly incoming = new Subject<ServerNotification>();

  private readonly state = signal<RealtimeStatus>('offline');

  readonly status = this.state.asReadonly();
  readonly isOnline = computed(() => this.state() === 'online');

  /** Последнее полученное сообщение; удобно для отладки и для шапки. */
  private readonly latest = signal<ServerNotification | null>(null);
  readonly lastNotification = this.latest.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => void this.stop());
  }

  /** Все сообщения подряд. */
  readonly messages$: Observable<ServerNotification> = this.incoming.asObservable();

  /**
   * Сообщения об одной сущности. Имя то же, что на сервере в журнале и в Notification.Entity.
   *
   * Применение на экране списка:
   *   this.realtime.onEntity('limit').pipe(takeUntilDestroyed()).subscribe(() => this.items.reload());
   */
  onEntity(entity: string): Observable<ServerNotification> {
    return this.incoming.pipe(filter(message => message.entity === entity));
  }

  async start(): Promise<void> {
    // Без роли портала хаб всё равно ответит 403 — не шумим
    if (this.connection || !this.auth.hasAnyRole()) {
      return;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(), {
        // Тот же заголовок, что ставит apiHeadersInterceptor: его проверяет UseApiProtection
        // на negotiate-запросе. В открытие WebSocket браузер заголовок положить не может —
        // там сервер полагается на Sec-Fetch-Site.
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
        withCredentials: true,
      })
      .withAutomaticReconnect(new StepRetryPolicy())
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on(NOTIFY_METHOD, (message: ServerNotification) => this.handle(message));

    connection.onreconnecting(() => this.state.set('reconnecting'));
    connection.onreconnected(() => this.state.set('online'));
    connection.onclose(() => this.state.set('offline'));

    this.connection = connection;
    this.state.set('connecting');

    try {
      await connection.start();
      this.state.set('online');
    } catch (error) {
      // Портал обязан работать и без хаба: это дополнение, а не условие работы
      this.state.set('offline');
      console.warn('SignalR: не вдалося підключитися', error);
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = undefined;

    if (connection && connection.state !== HubConnectionState.Disconnected) {
      try {
        await connection.stop();
      } catch {
        // закрывается при выгрузке страницы — результат уже никому не нужен
      }
    }

    this.state.set('offline');
  }

  private handle(message: ServerNotification): void {
    if (!message?.id || this.seen.has(message.id)) {
      return;
    }

    this.remember(message.id);
    this.latest.set(message);
    this.incoming.next(message);

    // Сообщение без текста — это сигнал экрану перечитать данные, показывать его не нужно
    if (!message.title) {
      return;
    }

    const text = message.message ? `${message.title}: ${message.message}` : message.title;
    if (message.kind === 'error' || message.kind === 'warning') {
      this.notifications.showError(text);
    } else {
      this.notifications.show(text);
    }
  }

  private remember(id: string): void {
    this.seen.add(id);
    if (this.seen.size > SEEN_LIMIT) {
      const oldest = this.seen.values().next().value;
      if (oldest !== undefined) {
        this.seen.delete(oldest);
      }
    }
  }
}

/**
 * Адрес хаба считается от <base href>: один и тот же билд работает и в корне сайта,
 * и в виртуальном приложении IIS. SignalR нужен абсолютный URL.
 */
function hubUrl(): string {
  return new URL(HUB_PATH, document.baseURI).toString();
}

class StepRetryPolicy implements IRetryPolicy {
  nextRetryDelayInMilliseconds(context: { previousRetryCount: number }): number {
    const index = Math.min(context.previousRetryCount, RETRY_DELAYS_MS.length - 1);
    return RETRY_DELAYS_MS[index];
  }
}
