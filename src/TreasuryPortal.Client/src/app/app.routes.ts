import { Routes } from '@angular/router';

import { AppPermissions } from '@core/auth/access.model';
import { authGuard, permissionGuard } from '@core/auth/auth.guards';
import { MainLayout } from '@layout/main-layout/main-layout';
import { StatusPage } from './pages/status-page/status-page';

const TITLE = 'Treasury Portal';

/**
 * Корневые маршруты.
 *
 * Разделы подключаются ленивыми loadChildren: чанк раздела не грузится, пока пользователь
 * туда не пошёл, а canMatch с правом не даёт загрузиться и чужому чанку.
 *
 * Новый раздел = папка в features/ со своим *.routes.ts + одна строка здесь + пункт в
 * layout/nav-items.ts. Права в обоих местах должны совпадать.
 */
export const routes: Routes = [
  {
    path: '',
    component: MainLayout,
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadChildren: () => import('@features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES),
      },
      {
        path: 'dictionaries',
        canMatch: [permissionGuard(AppPermissions.DictionaryRead)],
        loadChildren: () =>
          import('@features/dictionaries/dictionaries.routes').then(m => m.DICTIONARIES_ROUTES),
      },

      // Пример раздела сущности:
      // {
      //   path: 'payment-orders',
      //   canMatch: [permissionGuard(AppPermissions.PaymentOrderRead)],
      //   loadChildren: () =>
      //     import('@features/payment-orders/payment-orders.routes').then(m => m.PAYMENT_ORDERS_ROUTES),
      // },
    ],
  },

  // --- Служебные страницы: без меню, потому что в портал нас ещё не пустили ------------------
  {
    path: 'no-access',
    title: `Немає доступу · ${TITLE}`,
    component: StatusPage,
    data: {
      heading: 'Немає доступу',
      message:
        'Ваш доменний обліковий запис розпізнано, але жодної ролі в Treasury Portal для нього не налаштовано. Зверніться до адміністратора системи.',
      action: 'none',
      icon: 'lock_person',
    },
  },
  {
    path: 'forbidden',
    title: `Доступ заборонено · ${TITLE}`,
    component: StatusPage,
    data: {
      heading: 'Доступ заборонено',
      message: 'Цей розділ потребує прав, яких у вас немає.',
      icon: 'block',
    },
  },
  {
    path: 'auth-error',
    title: `Помилка входу · ${TITLE}`,
    component: StatusPage,
    data: {
      heading: 'Не вдалося увійти',
      message:
        'Доменна автентифікація не пройшла: вікно входу закрили, сервер недоступний або браузер не довіряє цьому сайту.',
      action: 'retry',
      icon: 'report',
    },
  },
  {
    path: '**',
    title: `Не знайдено · ${TITLE}`,
    component: StatusPage,
    data: {
      heading: 'Сторінку не знайдено',
      message: 'Перевірте адресу.',
      icon: 'search_off',
    },
  },
];
