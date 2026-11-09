/**
 * Роли и права портала. Имена должны совпадать с сервером (Web/Auth/AppAccess.cs) и с тем,
 * что заведено в базе.
 *
 * Добавляя сущность или процесс, впишите его права сюда — и больше нигде на клиенте
 * строковых имён прав быть не должно.
 */
export const AppRoles = {
  /** Доступ на просмотр: минимум, чтобы войти в портал. */
  Reader: 'TPReader',
  /** Право менять данные. */
  Writer: 'TPWriter',
  /** Администрирование портала. */
  Admin: 'TPAdmin',
} as const;

export type AppRole = (typeof AppRoles)[keyof typeof AppRoles];

export const AppPermissions = {
  DictionaryRead: 'TPDictionaryRead',
  AuditRead: 'TPAuditRead',

  // Пример для сущности:
  // PaymentOrderRead: 'TPPaymentOrderRead',
  // PaymentOrderWrite: 'TPPaymentOrderWrite',
} as const;

export type AppPermission = (typeof AppPermissions)[keyof typeof AppPermissions];

/** Ответ GET api/auth/me. */
export interface CurrentUser {
  /** DOMAIN\login */
  name: string;
  login: string;
  domain: string | null;
  fullName: string | null;
  /** Kerberos или NTLM — каким пакетом браузер реально прошёл аутентификацию. */
  authenticationType: string;
  roles: string[];
  permissions: string[];
  /** true — роли и права подменены секцией Auth:Dev (только Development). */
  devAccess: boolean;
}

export const EMPTY_USER: CurrentUser = {
  name: '',
  login: '',
  domain: null,
  fullName: null,
  authenticationType: '',
  roles: [],
  permissions: [],
  devAccess: false,
};
