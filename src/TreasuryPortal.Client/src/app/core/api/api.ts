/**
 * Все запросы к API — относительные (`api/...`, без ведущего слэша): браузер резолвит их
 * от <base href>. Сервер подставляет base href при отдаче index.html, поэтому один и тот же
 * билд работает и в корне сайта, и в виртуальном приложении IIS.
 *
 * Абсолютных адресов и environment.apiUrl в проекте нет и быть не должно: API живёт на том же
 * origin, что и клиент, — на этом держится и доменная аутентификация, и защита от CSRF.
 */
export const API_PREFIX = 'api/';

export function apiUrl(path: string): string {
  return API_PREFIX + path.replace(/^\/+/, '');
}

export function isApiRequest(url: string): boolean {
  return url.startsWith(API_PREFIX);
}
