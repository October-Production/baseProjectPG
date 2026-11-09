import { Pipe, PipeTransform } from '@angular/core';

import { formatAmount } from '../models/table-column';

/** Сумма с разделителями разрядов: 1 234 567,89. */
@Pipe({ name: 'amount' })
export class AmountPipe implements PipeTransform {
  transform(value: unknown, fractionDigits = 2): string {
    return formatAmount(value, fractionDigits);
  }
}
