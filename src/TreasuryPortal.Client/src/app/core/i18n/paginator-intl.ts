import { MatPaginatorIntl } from '@angular/material/paginator';

/**
 * Подписи пагинатора по-украински: по умолчанию Material отдаёт их по-английски,
 * и под таблицей портала это единственный английский текст на экране.
 *
 * Если понадобятся ещё подписи Material (сортировка, выбор даты) — заводите их здесь же.
 */
export function ukrainianPaginatorIntl(): MatPaginatorIntl {
  const intl = new MatPaginatorIntl();

  intl.itemsPerPageLabel = 'Рядків на сторінці:';
  intl.nextPageLabel = 'Наступна сторінка';
  intl.previousPageLabel = 'Попередня сторінка';
  intl.firstPageLabel = 'Перша сторінка';
  intl.lastPageLabel = 'Остання сторінка';

  intl.getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0 || pageSize === 0) {
      return `0 з ${length}`;
    }

    const total = Math.max(length, 0);
    const start = page * pageSize;
    const end = start < total ? Math.min(start + pageSize, total) : start + pageSize;

    return `${start + 1} – ${end} з ${total}`;
  };

  return intl;
}
