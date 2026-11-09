/** Одно значение справочника так, как его отдаёт процедура. */
export interface DictionaryItem {
  elementID: number | string;
  code?: string | null;
  name?: string | null;
  shortName?: string | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  [key: string]: unknown;
}

/** Ответ GET api/dictionaries/{name}. */
export interface DictionaryResponse {
  items?: DictionaryItem[];
}

/** Ответ GET api/dictionaries?names=…: словарь списков по имени справочника. */
export interface DictionariesResponse {
  dictionaries?: Record<string, DictionaryItem[]>;
}

/**
 * Имена справочников портала. Строковых имён в компонентах быть не должно —
 * иначе опечатку видно только в рантайме.
 */
export const Dictionaries = {
  Currency: 'currency',
  OperationType: 'operationType',
  Status: 'status',
} as const;

export type DictionaryName = (typeof Dictionaries)[keyof typeof Dictionaries];
