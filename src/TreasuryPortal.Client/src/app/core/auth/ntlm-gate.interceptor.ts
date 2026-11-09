import { HttpErrorResponse, HttpEvent, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, finalize, switchMap, throwError } from 'rxjs';

import { isApiRequest } from '../api/api';
import { Auth } from './auth';

/**
 * Метка сервера (Kestrel, NegotiateEvents.OnAuthenticationFailed): этот 400 — сбой handshake,
 * а не ошибка валидации.
 */
export const AUTH_RETRY_HEADER = 'X-Auth-Retry';

/**
 * Гейт для запросов к API: ждёт прогрев, а запрос, упавший на handshake, повторяет один раз
 * после общего перепрогрева.
 *
 * Ставить последним в withInterceptors — тогда повтор не увидят интерсепторы, которые стоят
 * раньше (обработка ошибок, уведомления): иначе пользователь получил бы сообщение об ошибке,
 * которой на самом деле не было.
 */
export const ntlmGateInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiRequest(req.url)) {
    return next(req); // статика и чужие хосты — без гейта
  }

  const auth = inject(Auth);

  const attempt = (isRetry: boolean): Observable<HttpEvent<unknown>> => {
    const warmup$ = auth.ready();
    let sent = false;

    return warmup$.pipe(
      switchMap(() => {
        sent = true;
        return next(req);
      }),
      catchError((error: unknown) => {
        // пока запрос не ушёл, упасть мог только прогрев (GET) — его повтор безопасен
        const failedMethod = sent ? req.method : 'GET';
        if (isRetry || !isHandshakeGlitch(error, failedMethod)) {
          return throwError(() => error);
        }
        auth.invalidate(warmup$);
        return attempt(true);
      }),
    );
  };

  return attempt(false).pipe(finalize(() => auth.touch()));
};

/**
 * Сбой handshake, а не ошибка приложения.
 *
 * Надёжный признак один — метка X-Auth-Retry, но её ставит только Kestrel: на IIS handshake
 * ведёт сам IIS и отвечает голым 400 без тела. Поэтому 400 на GET тоже считается сбоем — повтор
 * GET безопасен.
 *
 * Исключение: 400 с разобранным телом пришёл из нашего конвейера (ProblemDetails или конверт
 * `errors`), то есть это нормальная ошибка процедуры. Повторять её бессмысленно, а цена заметна:
 * лишний прогрев, лишний запрос и лишняя пара строк в журнале операций на каждую ошибку.
 *
 * 401 не повторяем: до JS он доходит, только если браузер не смог аутентифицироваться
 * (пользователь закрыл окно логина), и повтор лишь покажет окно ещё раз.
 */
function isHandshakeGlitch(error: unknown, method: string): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return false;
  }

  if (error.headers.has(AUTH_RETRY_HEADER)) {
    return true;
  }

  return method === 'GET' && !isApplicationError(error);
}

/** Тело, которое мог прислать только сервер приложения: ProblemDetails или конверт `errors`. */
function isApplicationError(error: HttpErrorResponse): boolean {
  const body: unknown = error.error;
  if (!body || typeof body !== 'object' || body instanceof ProgressEvent) {
    return false;
  }

  return ['title', 'detail', 'errors', 'error', 'message'].some(key => key in body);
}
