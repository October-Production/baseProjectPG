import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';

import { AlertDialog, AlertDialogData } from './alert-dialog/alert-dialog';
import { ConfirmDialog, ConfirmDialogData } from './confirm-dialog/confirm-dialog';

/**
 * Вопросы, которые задаёт любой экран. Благодаря этому страница никогда не открывает диалог
 * руками ради «да/нет» или сообщения о результате.
 */
@Injectable({ providedIn: 'root' })
export class DialogService {
  private readonly dialog = inject(MatDialog);

  confirm(message: string, options: Partial<ConfirmDialogData> = {}): Promise<boolean> {
    const ref = this.dialog.open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
      disableClose: true,
      autoFocus: true,
      width: '480px',
      data: { message, ...options },
    });

    return firstValueFrom(ref.afterClosed()).then(result => !!result);
  }

  /** Вопрос перед удалением: подтверждающая кнопка красная. */
  confirmDelete(message: string): Promise<boolean> {
    return this.confirm(message, {
      title: 'Увага',
      confirmText: 'Видалити',
      cancelText: 'Відміна',
      danger: true,
    });
  }

  /** Вопрос при уходе со страницы с несохранёнными правками (pendingChangesGuard). */
  confirmLeave(): Promise<boolean> {
    return this.confirm('На сторінці є незбережені зміни. Залишити сторінку?', {
      title: 'Незбережені зміни',
      confirmText: 'Залишити',
      cancelText: 'Повернутись',
      danger: true,
    });
  }

  message(title: string, message: string, width = '520px'): Promise<void> {
    const ref = this.dialog.open<AlertDialog, AlertDialogData>(AlertDialog, {
      disableClose: true,
      autoFocus: true,
      width,
      data: { title, message },
    });

    return firstValueFrom(ref.afterClosed()).then(() => undefined);
  }
}
