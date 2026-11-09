import { Pipe, PipeTransform } from '@angular/core';

import { formatDate } from '../utils/date-utils';

/** Дата так, как её пишет бизнес: 31.12.2026. */
@Pipe({ name: 'appDate' })
export class AppDatePipe implements PipeTransform {
  transform(value: Date | string | null | undefined): string {
    return formatDate(value);
  }
}
