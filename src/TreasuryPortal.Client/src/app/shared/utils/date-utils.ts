/** Дата в том виде, в каком её пишет бизнес: 31.12.2026. */
export function formatDate(value: Date | string | null | undefined): string {
  const date = toDate(value);
  if (!date) {
    return '';
  }
  return new Intl.DateTimeFormat('uk-UA', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(date);
}

/** Полночь указанного дня, чтобы сравнения не зависели от времени. */
export function startOfDay(date: Date): Date {
  const value = new Date(date);
  value.setHours(0, 0, 0, 0);
  return value;
}

/** Даты приходят с сервера строками; пустое или неразбираемое значение становится null. */
export function toDate(value: Date | string | null | undefined): Date | null {
  if (value == null || value === '') {
    return null;
  }
  const date = value instanceof Date ? value : new Date(value);
  return isNaN(date.getTime()) ? null : date;
}

/**
 * Процедуры сохранения читают даты как MM/dd/yyyy — именно таким текстом их и отправляем,
 * чтобы преобразование на стороне базы не зависело от часового пояса браузера.
 */
export function toServerDate(value: Date | null | undefined): string | null {
  if (!value) {
    return null;
  }
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${month}/${day}/${value.getFullYear()}`;
}

/** Действует ли запись сегодня: уже началась и ещё не закончилась. */
export function isActiveToday(
  dateFrom: Date | null | undefined,
  dateTo: Date | null | undefined,
  startRequired = false,
): boolean {
  const today = startOfDay(new Date()).getTime();

  if (!dateFrom) {
    if (startRequired) {
      return false;
    }
  } else if (startOfDay(dateFrom).getTime() > today) {
    return false;
  }

  return !dateTo || startOfDay(dateTo).getTime() >= today;
}

/** Конец периода должен быть строго позже начала. */
export function isPeriodValid(dateFrom: Date | null | undefined, dateTo: Date | null | undefined): boolean {
  if (!dateFrom || !dateTo) {
    return true;
  }
  return startOfDay(dateFrom).getTime() < startOfDay(dateTo).getTime();
}
