/** Разделитель, который не может встретиться в поисковом запросе. */
const FIELD_SEPARATOR = '(#$#)';

/**
 * Текст, по которому строка списка находится поиском: выбранные поля, склеенные разделителем,
 * которого в запросе быть не может, — поэтому запрос никогда не совпадёт «через границу» полей.
 * Даты в поиск не попадают.
 */
export function buildSearchString(item: object, fields: readonly string[]): string {
  const values = item as Record<string, unknown>;
  return fields
    .map(field => values[field])
    .filter(value => value != null && value !== '' && !(value instanceof Date))
    .map(value => String(value))
    .join(FIELD_SEPARATOR)
    .toLowerCase();
}

export function matchesSearch(searchString: string | undefined, query: string): boolean {
  const text = query.trim().toLowerCase();
  return !text || (searchString ?? '').includes(text);
}
