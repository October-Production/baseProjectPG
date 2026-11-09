import { ServiceError } from '@core/errors/service-error.model';

/** Любой эндпоинт записи отвечает `{ res: { result, ... } }`. */
export interface ActionResult {
  result?: number | boolean | string;
  [key: string]: unknown;
}

export interface ActionResponse {
  res?: ActionResult;
  errors?: ServiceError[];
}

export function isSuccess(result: ActionResult | null | undefined): boolean {
  return !!result?.result;
}

/** Текст первой ошибки или общий, если процедура его не дала. */
export function firstErrorMessage(response: ActionResponse | null | undefined): string {
  const error = response?.errors?.[0];
  return error?.message?.trim() || error?.code || 'Невідома помилка';
}
