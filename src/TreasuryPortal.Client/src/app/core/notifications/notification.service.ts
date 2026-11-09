import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

import { ServiceError, errorsToText } from '../errors/service-error.model';

/**
 * Короткие сообщения поверх экрана. Ошибка висит дольше обычного сообщения: её читают и
 * пересказывают сопровождению, и исчезнуть раньше она не должна.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  show(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 5000 });
  }

  showError(message: string): void {
    this.snackBar.open(message, 'OK', { duration: 30000, panelClass: 'tp-snack-error' });
  }

  /** Ошибки, пришедшие внутри успешного ответа. */
  showServiceErrors(errors: ServiceError[]): void {
    if (!errors.length) {
      return;
    }
    this.showError(errorsToText(errors) || 'Невідома помилка');
  }
}
