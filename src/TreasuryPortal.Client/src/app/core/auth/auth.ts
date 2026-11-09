import { HttpBackend, HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';

import { apiUrl } from '../api/api';
import { AppPermission, AppRole, CurrentUser } from './access.model';

/** Эндпоинт прогрева: под Windows-аутентификацией, дешёвый, отдаёт текущего пользователя. */
export const AUTH_WARMUP_URL = apiUrl('auth/me');

/**
 * Простой, после которого прогрев повторяется. Чуть меньше самого короткого keep-alive на пути:
 * IIS — 120 с, Kestrel — 130 с; если впереди балансер или прокси — сверьтесь с его таймаутом.
 */
export const AUTH_IDLE_MS = 90_000;

/**
 * Текущий доменный пользователь, его права и NTLM-гейт.
 *
 * NTLM аутентифицирует не запрос, а TCP-соединение: параллельные запросы на новых соединениях
 * проходят handshake каждый сам по себе. Гейт пускает первым один прогревочный запрос — креды
 * резолвятся один раз (одно окно логина, если оно вообще появится), остальные запросы ждут его.
 * Ответ прогрева — это и есть текущий пользователь вместе с ролями и правами.
 *
 * Права приходят один раз при старте и дальше читаются из сигналов: отдельных запросов
 * «можно ли мне X» на каждую страницу нет.
 */
@Injectable({ providedIn: 'root' })
export class Auth {
  /** Мимо интерсепторов: прогрев не должен гейтить сам себя. */
  private readonly http = new HttpClient(inject(HttpBackend));
  private readonly current = signal<CurrentUser | null>(null);
  private warmup$?: Observable<CurrentUser>;
  private lastActivity = 0;

  /** null — прогрев ещё не завершился или упал. */
  readonly user = this.current.asReadonly();

  readonly isLoaded = computed(() => this.current() !== null);

  readonly login = computed(() => this.current()?.login ?? '');

  readonly displayName = computed(() => {
    const user = this.current();
    return user?.fullName || user?.login || '';
  });

  private readonly roleSet = computed(() => new Set(this.current()?.roles ?? []));
  private readonly permissionSet = computed(() => new Set(this.current()?.permissions ?? []));

  /** Есть ли у пользователя хоть одна роль портала: без неё внутрь не пускаем. */
  readonly hasAnyRole = computed(() => this.roleSet().size > 0);

  hasRole(...roles: AppRole[]): boolean {
    const set = this.roleSet();
    return roles.some(role => set.has(role));
  }

  can(...permissions: AppPermission[]): boolean {
    const set = this.permissionSet();
    return permissions.some(permission => set.has(permission));
  }

  /** Один общий прогрев на все запросы; после простоя — свежий. */
  ready(): Observable<CurrentUser> {
    if (!this.warmup$ || Date.now() - this.lastActivity > AUTH_IDLE_MS) {
      // shareReplay сбрасывается при ошибке: упавший прогрев повторится на следующем запросе
      this.warmup$ = this.http.get<CurrentUser>(AUTH_WARMUP_URL).pipe(
        tap(user => this.current.set(user)),
        shareReplay(1),
      );
    }
    this.touch();
    return this.warmup$;
  }

  /**
   * Сбрасывает только тот прогрев, после которого упали:
   * сколько бы запросов ни упало разом, перепрогрев будет один.
   */
  invalidate(stale: Observable<CurrentUser>): void {
    if (this.warmup$ === stale) {
      this.warmup$ = undefined;
    }
  }

  touch(): void {
    this.lastActivity = Date.now();
  }
}
