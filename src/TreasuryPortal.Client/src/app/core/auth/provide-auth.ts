import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Auth } from './auth';

/**
 * Прогрев на старте — при каждой полной загрузке страницы, с какого бы роута она ни началась.
 * Начальная навигация (guard'ы, resolver'ы, компоненты) стартует после инициализаторов, поэтому
 * к первому рендеру роли и права уже известны и экран не моргает «нет доступа».
 *
 * Упавший прогрев старт не валит: guard'ы повторят его и при неудаче отправят на /auth-error.
 */
export function provideAuth(): EnvironmentProviders {
  return provideAppInitializer(() =>
    firstValueFrom(inject(Auth).ready()).then(
      () => undefined,
      () => undefined,
    ),
  );
}
