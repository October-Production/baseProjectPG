import { AbstractControl, FormGroup } from '@angular/forms';

/**
 * Ошибка поля показывается, когда пользователь из него ушёл или когда форму уже пытались
 * сохранить: при первом открытии карточки красным не светится ничего.
 */
export function showFieldError(control: AbstractControl | null, submitted: boolean): boolean {
  return !!control && control.invalid && (control.touched || submitted);
}

/** Помечает все поля тронутыми — вызывается при неудачной попытке сохранить. */
export function markAllTouched(form: FormGroup): void {
  form.markAllAsTouched();
  form.updateValueAndValidity({ onlySelf: false, emitEvent: false });
}

/** Первое невалидное поле формы — чтобы прокрутить к нему экран. */
export function firstInvalidControlName(form: FormGroup): string | null {
  for (const [name, control] of Object.entries(form.controls)) {
    if (control.invalid) {
      return name;
    }
  }
  return null;
}
