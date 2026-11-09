import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { Auth } from '@core/auth/auth';
import { Realtime } from './realtime.service';

/**
 * Подключение к хабу после прогрева доменной аутентификации.
 *
 * Порядок важен. SignalR ходит мимо HttpClient, а значит мимо NTLM-гейта: если открыть хаб
 * раньше прогрева, его negotiate уйдёт одновременно с первыми запросами страницы и запустит
 * отдельный Negotiate-handshake — ровно то, от чего гейт и защищает.
 *
 * Старт не блокирует загрузку портала: подключение поднимается в фоне, и неудача его не валит —
 * экраны работают обычными запросами, просто без мгновенных обновлений.
 */
export function provideRealtime(): EnvironmentProviders {
  return provideAppInitializer(() => {
    const auth = inject(Auth);
    const realtime = inject(Realtime);

    void firstValueFrom(auth.ready())
      .then(() => realtime.start())
      .catch(() => undefined);

    return undefined;
  });
}
