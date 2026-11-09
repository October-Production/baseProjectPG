/** Простая копия модели для запроса сохранения; Date превращается в ISO-текст. */
export function toPayload<T extends object>(value: T): Record<string, unknown> {
  return JSON.parse(JSON.stringify(value)) as Record<string, unknown>;
}

/**
 * Текстовое поле, которое пользователь оставил пустым, уходит на сервер так, как пришло:
 * значение, которого не было, остаётся null (процедура его не трогает), а очищенное
 * отправляется пустой строкой.
 */
export function keepUnset(value: string, original: string | null | undefined): string | null | undefined {
  return value === '' && original == null ? original : value;
}

/** Нестрогое сравнение кодов: одна процедура отдаёт их числом, другая строкой. */
export function sameCode(a: unknown, b: unknown): boolean {
  return a == b;
}
