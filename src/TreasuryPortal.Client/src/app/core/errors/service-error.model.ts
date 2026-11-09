/**
 * Конверт ошибок, которым отвечают хранимые процедуры: массив `errors` рядом с полезной
 * нагрузкой. Частичный результат приходит вместе со своими предупреждениями, поэтому наличие
 * `errors` само по себе не означает провал запроса.
 */
export type ServiceErrorType = 'err' | 'worn';

export interface ServiceError {
  type: ServiceErrorType;
  code: string;
  message?: string;
  details?: string;
}

export interface ErrorEnvelope {
  errors?: ServiceError[];
}

export function readErrors(body: unknown): ServiceError[] {
  if (!body || typeof body !== 'object') {
    return [];
  }
  const errors = (body as ErrorEnvelope).errors;
  return Array.isArray(errors) ? errors : [];
}

export function errorsToText(errors: ServiceError[]): string {
  return errors
    .map(error => error.message?.trim() || error.code)
    .filter(Boolean)
    .join('; ');
}
