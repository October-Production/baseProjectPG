import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export interface ConfirmDialogData {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  /** Опасное действие (удаление): подтверждающая кнопка красная. */
  danger?: boolean;
}

@Component({
  selector: 'tp-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title ?? 'Підтвердження' }}</h2>
    <mat-dialog-content>
      <p class="tp-dialog-message">{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton (click)="dialogRef.close(false)">{{ data.cancelText ?? 'Відміна' }}</button>
      <button
        matButton="filled"
        cdkFocusInitial
        [class.tp-danger]="data.danger"
        (click)="dialogRef.close(true)"
      >
        {{ data.confirmText ?? 'Так' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .tp-danger {
      --mat-sys-primary: var(--mat-sys-error);
      --mat-sys-on-primary: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmDialog {
  readonly dialogRef = inject<MatDialogRef<ConfirmDialog, boolean>>(MatDialogRef);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
