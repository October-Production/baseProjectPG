import { formatDate } from '../utils/date-utils';

/** Одна обычная колонка списка. */
export interface TableColumn {
  key: string;
  name: string;
  /** Ширина колонки, например '12%'. */
  width?: string;
  type?: 'text' | 'number' | 'amount' | 'date' | 'yesNo';
}

/** Текст обычной ячейки: даты как 31.12.2026, флаги как Так / Ні, суммы с разделителями. */
export function cellText(row: object, column: TableColumn): string {
  const value = (row as Record<string, unknown>)[column.key];

  switch (column.type) {
    case 'date':
      return formatDate(value as Date | string | null);
    case 'yesNo':
      return value ? 'Так' : 'Ні';
    case 'amount':
      return formatAmount(value);
    default:
      return value == null ? '' : String(value);
  }
}

/** Сумма с разделителями разрядов и двумя знаками: 1 234 567,89. */
export function formatAmount(value: unknown, fractionDigits = 2): string {
  const amount = typeof value === 'number' ? value : Number(value);
  if (value == null || value === '' || isNaN(amount)) {
    return '';
  }
  return new Intl.NumberFormat('uk-UA', {
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  }).format(amount);
}

/** CSS-класс колонки: числа и суммы выравниваются вправо. */
export function cellClass(column: TableColumn): string {
  return column.type === 'number' || column.type === 'amount' ? 'tp-num' : '';
}
