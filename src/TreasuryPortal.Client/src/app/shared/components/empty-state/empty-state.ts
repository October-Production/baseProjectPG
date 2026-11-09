import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/**
 * Пустой список. Отдельный компонент, чтобы «нічого не знайдено» везде выглядело одинаково
 * и его нельзя было спутать с не загрузившейся таблицей.
 */
@Component({
  selector: 'tp-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <div class="empty">
      <mat-icon>{{ icon() }}</mat-icon>
      <p>{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 48px 16px;
      color: var(--mat-sys-on-surface-variant);
      text-align: center;
    }

    mat-icon {
      --mat-icon-color: currentColor;
      width: 40px;
      height: 40px;
      font-size: 40px;
      opacity: 0.5;
    }

    p {
      margin: 0;
    }
  `,
})
export class EmptyState {
  readonly message = input('Нічого не знайдено');
  readonly icon = input('inbox');
}
