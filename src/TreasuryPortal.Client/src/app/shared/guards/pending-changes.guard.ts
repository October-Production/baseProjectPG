import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';

import { DialogService } from '@core/dialogs/dialog.service';

/** Страница, на которой может остаться несохранённая работа, реализует это. */
export interface HasPendingChanges {
  /** true — уходить можно без вопросов. */
  hasPendingChanges: () => boolean;
}

/**
 * Спрашивает подтверждение перед уходом со страницы с несохранёнными правками.
 * Вешается на маршрут карточки: canDeactivate: [pendingChangesGuard].
 */
export const pendingChangesGuard: CanDeactivateFn<HasPendingChanges> = component => {
  if (!component?.hasPendingChanges?.()) {
    return true;
  }

  return inject(DialogService).confirmLeave();
};
