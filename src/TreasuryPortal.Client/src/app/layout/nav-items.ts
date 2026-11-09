import { AppPermission, AppRole } from '@core/auth/access.model';

/**
 * Пункт бокового меню. Пункт показывается, только если у пользователя есть хоть одно из
 * перечисленных прав (или ролей) — поэтому меню само по себе не врёт о доступности раздела.
 *
 * Пункты без permission и role видны всем, кто вообще вошёл в портал.
 */
export interface NavItem {
  path: string;
  title: string;
  /** Имя иконки Material Icons. */
  icon: string;
  permissions?: AppPermission[];
  roles?: AppRole[];
}

/**
 * Меню портала. Добавляя раздел, впишите его сюда и в app.routes.ts — права в обоих местах
 * должны совпадать, иначе пользователь увидит пункт, который приведёт его на /forbidden.
 */
export const NAV_ITEMS: readonly NavItem[] = [
  {
    path: '/dashboard',
    title: 'Огляд',
    icon: 'dashboard',
  },
  {
    path: '/dictionaries',
    title: 'Довідники',
    icon: 'list_alt',
    permissions: ['TPDictionaryRead'],
  },

  // Пример раздела сущности:
  // {
  //   path: '/payment-orders',
  //   title: 'Платіжні доручення',
  //   icon: 'receipt_long',
  //   permissions: ['TPPaymentOrderRead'],
  // },
];
