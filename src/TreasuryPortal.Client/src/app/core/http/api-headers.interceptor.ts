import { HttpInterceptorFn } from '@angular/common/http';

import { isApiRequest } from '../api/api';

/**
 * CSRF-защита без cookie. Windows-аутентификация «амбиентная»: браузер сам проходит handshake
 * и для запроса, который отправила чужая страница. Поэтому сервер принимает изменяющие запросы
 * к API только с этим заголовком: кросс-сайтовая форма его поставить не может, а fetch с чужого
 * origin с таким заголовком требует CORS-preflight, которого сервер не разрешает.
 *
 * Парная проверка на сервере — Web/Http/ApiProtection.cs.
 */
export const apiHeadersInterceptor: HttpInterceptorFn = (req, next) =>
  next(isApiRequest(req.url) ? req.clone({ setHeaders: { 'X-Requested-With': 'XMLHttpRequest' } }) : req);
