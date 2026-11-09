import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Шапка экрана: заголовок, подпись и место под кнопки действий.
 * Нужна, чтобы все страницы начинались одинаково и кнопки стояли в одном месте.
 *
 * Применение:
 *   <tp-page-header title="Ліміти" subtitle="Діючі на сьогодні">
 *     <button matButton="filled">Додати</button>
 *   </tp-page-header>
 */
@Component({
  selector: 'tp-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="tp-toolbar-row">
      <div class="titles">
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p class="tp-muted">{{ subtitle() }}</p>
        }
      </div>
      <span class="tp-spacer"></span>
      <ng-content />
    </header>
  `,
  styles: `
    .titles {
      min-width: 0;
    }

    h1 {
      margin: 0;
      font: var(--mat-sys-headline-small);
    }

    p {
      margin: 2px 0 0;
      font: var(--mat-sys-body-small);
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
}
