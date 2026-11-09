import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { Auth } from '@core/auth/auth';
import { Realtime } from '@core/realtime/realtime.service';
import { ThemeService } from '@core/theme/theme.service';
import { NAV_ITEMS, NavItem } from '../nav-items';

const SIDENAV_KEY = 'tp-sidenav-collapsed';

/**
 * Каркас портала: верхняя панель, боковое меню и место под страницу.
 *
 * Меню строится из NAV_ITEMS и фильтруется правами пользователя, поэтому лишних пунктов
 * на экране не бывает. Свёрнутое состояние запоминается: казначей работает в широких таблицах
 * и обычно держит меню свёрнутым.
 */
@Component({
  selector: 'tp-main-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    MatTooltipModule,
    MatDividerModule,
  ],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.scss',
})
export class MainLayout {
  private readonly auth = inject(Auth);
  private readonly themeService = inject(ThemeService);
  private readonly realtime = inject(Realtime);

  protected readonly collapsed = signal(readCollapsed());

  /** Состояние связи с сервером: точка в шапке. */
  protected readonly realtimeStatus = this.realtime.status;

  protected readonly realtimeHint = computed(
    () =>
      ({
        online: "Зв'язок із сервером активний: зміни надходять одразу",
        connecting: 'Підключення до сервера…',
        reconnecting: "Зв'язок перервано, відновлюємо…",
        offline: "Немає зв'язку з сервером: оновлюйте сторінку вручну",
      })[this.realtimeStatus()],
  );

  protected readonly theme = this.themeService.theme;
  protected readonly displayName = this.auth.displayName;
  protected readonly login = this.auth.login;

  protected readonly user = this.auth.user;

  /** Пункты, на которые у пользователя есть права. */
  protected readonly items = computed(() => {
    // user() в зависимостях — список пересчитается, когда придут права
    this.auth.user();
    return NAV_ITEMS.filter(item => this.isAllowed(item));
  });

  protected toggleSidenav(): void {
    this.collapsed.update(value => {
      const next = !value;
      try {
        localStorage.setItem(SIDENAV_KEY, String(next));
      } catch {
        // приватный режим — состояние просто не запомнится
      }
      return next;
    });
  }

  protected toggleTheme(): void {
    this.themeService.toggle();
  }

  private isAllowed(item: NavItem): boolean {
    if (item.permissions?.length && !this.auth.can(...item.permissions)) {
      return false;
    }
    if (item.roles?.length && !this.auth.hasRole(...item.roles)) {
      return false;
    }
    return true;
  }
}

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(SIDENAV_KEY) === 'true';
  } catch {
    return false;
  }
}
