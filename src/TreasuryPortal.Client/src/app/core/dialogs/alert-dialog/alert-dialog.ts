import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

export interface AlertDialogData {
  title: string;
  message: string;
}

@Component({
  selector: 'tp-alert-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p class="tp-dialog-message">{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton="filled" cdkFocusInitial mat-dialog-close>OK</button>
    </mat-dialog-actions>
  `,
})
export class AlertDialog {
  readonly data = inject<AlertDialogData>(MAT_DIALOG_DATA);
}
