import { Pipe, PipeTransform } from '@angular/core';

/** Флаг как Так / Ні — так его читают во всех таблицах портала. */
@Pipe({ name: 'yesNo' })
export class YesNoPipe implements PipeTransform {
  transform(value: unknown): string {
    return value ? 'Так' : 'Ні';
  }
}
