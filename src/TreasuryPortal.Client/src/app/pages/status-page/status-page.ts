import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

export type StatusAction = 'home' | 'retry' | 'none';

/**
 * Служебная страница: нет доступа, заборонено, помилка входу, не знайдено.
 * Тексты приходят из data маршрута через withComponentInputBinding(), поэтому на каждый случай
 * не нужен свой компонент.
 */
@Component({
  selector: 'tp-status-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule],
  template: `
    <section class="status">
      <mat-icon>{{ icon() }}</mat-icon>
      <h1>{{ heading() }}</h1>
      <p>{{ message() }}</p>

      @switch (action()) {
        @case ('home') {
          <a matButton="filled" routerLink="/">На головну</a>
        }
        @case ('retry') {
          <button matButton="filled" type="button" (click)="retry()">Спробувати ще раз</button>
        }
      }
    </section>
  `,
  styles: `
    .status {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      max-width: 520px;
      margin: 18vh auto 0;
      padding: 0 16px;
      text-align: center;
    }

    mat-icon {
      --mat-icon-color: var(--mat-sys-on-surface-variant);
      width: 48px;
      height: 48px;
      font-size: 48px;
      opacity: 0.6;
    }

    h1 {
      margin: 0;
      font: var(--mat-sys-headline-small);
    }

    p {
      margin: 0;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class StatusPage {
  readonly heading = input('');
  readonly message = input('');
  readonly action = input<StatusAction>('home');
  readonly icon = input('info');

  /** Полная загрузка с корня приложения: заново прогрев на старте и проверка прав. */
  protected retry(): void {
    location.assign(document.baseURI);
  }
}
