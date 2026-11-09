import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router, UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';

import { AppPermission, AppRole } from './access.model';
import { Auth } from './auth';

/**
 * Пускает пользователя с любой ролью портала; доменный пользователь без ролей — на /no-access.
 * Вешается один раз на корневой маршрут, отдельным страницам не нужен.
 */
export const authGuard: CanActivateFn = () => check(auth => auth.hasAnyRole(), '/no-access');

/**
 * Пускает только указанные роли. Это canMatch: чанк чужой страницы даже не загрузится.
 *
 * Роль отвечает за «кто вы», право — за «что вам можно». Для страниц сущностей берите
 * permissionGuard: он переживёт изменение состава ролей.
 */
export function roleGuard(...roles: AppRole[]): CanMatchFn {
  return () => check(auth => auth.hasRole(...roles), '/forbidden');
}

/** Пускает, если есть хотя бы одно из перечисленных прав. */
export function permissionGuard(...permissions: AppPermission[]): CanMatchFn {
  return () => check(auth => auth.can(...permissions), '/forbidden');
}

/** Через ready(): после простоя навигация сама запустит прогрев раньше запросов страницы. */
function check(allowed: (auth: Auth) => boolean, deniedUrl: string): Observable<boolean | UrlTree> {
  const auth = inject(Auth);
  const router = inject(Router);

  return auth.ready().pipe(
    map(() => allowed(auth) || router.parseUrl(deniedUrl)),
    catchError(() => of(router.parseUrl('/auth-error'))),
  );
}
