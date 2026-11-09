import {
  HttpContextToken,
  HttpErrorResponse,
  HttpEvent,
  HttpInterceptorFn,
  HttpResponse,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, map, throwError } from 'rxjs';

import { errorsToText, readErrors } from '../errors/service-error.model';
import { NotificationService } from '../notifications/notification.service';

/**
 * Ставится на запрос, автор которого сам разбирает `errors` успешного ответа — например,
 * групповое удаление собирает их по записям в одну сводку вместо уведомления на каждую.
 */
export const COLLECT_SERVICE_ERRORS = new HttpContextToken<boolean>(() => false);

/**
 * Единственное место, где неудачный вызов превращается в читаемое сообщение, — поэтому сервисы
 * и компоненты имеют дело только с данными.
 *
 * Сюда приходят два вида неудач: настоящая ошибка HTTP и ответ 200 с массивом `errors` —
 * так отвечают хранимые процедуры.
 */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifications = inject(NotificationService);
  const collectErrors = req.context.get(COLLECT_SERVICE_ERRORS);

  return next(req).pipe(
    map((event: HttpEvent<unknown>) => {
      if (collectErrors || !(event instanceof HttpResponse)) {
        return event;
      }

      const errors = readErrors(event.body);
      if (!errors.length) {
        return event;
      }

      notifications.showServiceErrors(errors);

      // Полезная нагрузка остаётся пригодной: здесь снимается только конверт
      const { errors: _consumed, ...payload } = event.body as Record<string, unknown>;
      return event.clone({ body: payload });
    }),
    catchError((error: HttpErrorResponse) => {
      console.error(error);
      notifications.showError(describeHttpError(error));
      return throwError(() => error);
    }),
  );
};

export function describeHttpError(error: HttpErrorResponse): string {
  const text = describeBody(error.error);
  switch (error.status) {
    case 0:
      return `Сервер недоступний: ${text}`;
    case 400:
      return `Помилка при виконанні дії: ${text}`;
    case 401:
      return 'Не вдалося підтвердити доменний обліковий запис';
    case 403:
      return `Відмовлено в доступі: ${text}`;
    case 404:
      return `Ресурс не знайдено: ${text}`;
    case 409:
      return text;
    case 422:
      return `Помилка валідації: ${text}`;
    case 500:
      return `Серверна помилка: ${text}`;
    case 502:
    case 503:
    case 504:
      return `Сервер недоступний: ${text}`;
    default:
      return `Невідома помилка: ${text}`;
  }
}

function describeBody(raw: unknown): string {
  if (!raw) {
    return 'Текст відсутній';
  }

  const errors = readErrors(raw);
  if (errors.length) {
    return errorsToText(errors);
  }

  if (raw instanceof ProgressEvent) {
    return "Немає з'єднання";
  }

  if (typeof raw === 'string') {
    try {
      const parsed: unknown = JSON.parse(raw);
      const parsedErrors = readErrors(parsed);
      return parsedErrors.length ? errorsToText(parsedErrors) : raw;
    } catch {
      return raw;
    }
  }

  // ProblemDetails от ASP.NET: { title, status, detail }
  if (typeof raw === 'object') {
    const body = raw as Record<string, unknown>;
    for (const key of ['detail', 'title', 'error', 'message']) {
      const value = body[key];
      if (typeof value === 'string' && value.trim()) {
        return value;
      }
    }
  }

  return String(raw);
}
