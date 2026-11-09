import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';

import { Auth } from '@core/auth/auth';
import { PageHeader } from '@shared/components/page-header/page-header';

/**
 * Стартовый экран. Пока показывает, кто вошёл и какие права получил, — этого достаточно,
 * чтобы убедиться, что доменная аутентификация и выдача прав работают.
 * Содержимое заменяется, как только появятся первые процессы казначейства.
 */
@Component({
  selector: 'tp-dashboard-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatCardModule, MatChipsModule, MatIconModule, PageHeader],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.scss',
})
export class DashboardPage {
  private readonly auth = inject(Auth);

  protected readonly user = this.auth.user;
  protected readonly displayName = this.auth.displayName;
}
