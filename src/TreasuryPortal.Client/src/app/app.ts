import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * Корень приложения. Намеренно пустой: каркас портала — это MainLayout, и он подключён
 * маршрутом, чтобы служебные страницы (нет доступа, ошибка входа) открывались без меню.
 */
@Component({
  selector: 'tp-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: `
    :host {
      display: block;
      height: 100%;
    }
  `,
})
export class App {}
