import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MAT_PAGINATOR_DEFAULT_OPTIONS, MatPaginatorIntl } from '@angular/material/paginator';
import { MAT_TOOLTIP_DEFAULT_OPTIONS } from '@angular/material/tooltip';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';

import { provideAuth } from '@core/auth/provide-auth';
import { ukrainianPaginatorIntl } from '@core/i18n/paginator-intl';
import { provideRealtime } from '@core/realtime/provide-realtime';
import { ntlmGateInterceptor } from '@core/auth/ntlm-gate.interceptor';
import { apiErrorInterceptor } from '@core/http/api-error.interceptor';
import { apiHeadersInterceptor } from '@core/http/api-headers.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideBrowserGlobalErrorListeners(),

    provideRouter(
      routes,
      // data маршрута приходит во input'ы компонента — на этом держатся служебные страницы
      withComponentInputBinding(),
      withInMemoryScrolling({ scrollPositionRestoration: 'enabled', anchorScrolling: 'enabled' }),
    ),

    // Порядок важен. Гейт — последним: повтор после сбоя handshake не должен проходить
    // повторно через интерсептор ошибок, иначе пользователь увидит сообщение об ошибке,
    // которой не было.
    provideHttpClient(
      withFetch(),
      withInterceptors([apiHeadersInterceptor, apiErrorInterceptor, ntlmGateInterceptor]),
    ),

    // Прогрев доменной аутентификации и загрузка прав до первой навигации
    provideAuth(),

    // Подключение к хабу — после прогрева, в фоне
    provideRealtime(),

    // Даты вводятся и показываются так, как их пишет бизнес: 31.12.2026
    provideNativeDateAdapter(),
    { provide: MAT_DATE_LOCALE, useValue: 'uk-UA' },

    // Поля одного вида во всём портале — иначе каждая карточка выглядит по-своему
    {
      provide: MAT_FORM_FIELD_DEFAULT_OPTIONS,
      useValue: { appearance: 'outline', subscriptSizing: 'dynamic' },
    },
    {
      provide: MAT_PAGINATOR_DEFAULT_OPTIONS,
      useValue: { pageSize: 25, pageSizeOptions: [25, 50, 100, 200] },
    },
    // Подписи пагинатора по-украински: по умолчанию Material отдаёт их по-английски
    { provide: MatPaginatorIntl, useFactory: ukrainianPaginatorIntl },
    {
      provide: MAT_TOOLTIP_DEFAULT_OPTIONS,
      useValue: { showDelay: 400, hideDelay: 0, touchendHideDelay: 1000 },
    },
  ],
};
