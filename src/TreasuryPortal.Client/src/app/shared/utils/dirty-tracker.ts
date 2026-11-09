import { Signal, computed, signal } from '@angular/core';

/**
 * Помнит, как выглядела форма при загрузке или после последнего сохранения, — чтобы при уходе
 * со страницы спрашивать подтверждение, только если действительно что-то изменилось.
 *
 * Сравнение идёт по JSON: быстро, не требует описания модели и переживает вложенные списки.
 */
export class DirtyTracker<T> {
  private readonly snapshot = signal('');

  readonly isDirty: Signal<boolean>;

  constructor(model: Signal<T>) {
    this.isDirty = computed(() => JSON.stringify(model()) !== this.snapshot());
    this.snapshot.set(JSON.stringify(model()));
  }

  /** Вызывается после загрузки и после успешного сохранения. */
  markClean(model: Signal<T>): void {
    this.snapshot.set(JSON.stringify(model()));
  }
}
