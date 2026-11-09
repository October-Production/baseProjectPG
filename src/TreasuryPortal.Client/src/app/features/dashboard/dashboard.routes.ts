import { Routes } from '@angular/router';

/**
 * Маршруты раздела. Каждый раздел описывает свои маршруты сам — корневой app.routes.ts
 * знает о нём одну строку и ничего не знает о его страницах.
 */
export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    title: 'Огляд · Treasury Portal',
    loadComponent: () => import('./pages/dashboard-page/dashboard-page').then(m => m.DashboardPage),
  },
];
