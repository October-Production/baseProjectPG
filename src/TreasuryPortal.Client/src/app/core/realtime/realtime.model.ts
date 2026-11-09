/** Вид сообщения — то же перечисление, что на сервере (Web/Hubs/Notification.cs). */
export type NotificationKind = 'info' | 'success' | 'warning' | 'error';

/**
 * Сообщение от сервера. В нём намеренно нет данных сущности — только повод их перечитать:
 * получатель мог не иметь права видеть изменившуюся запись, поэтому клиент перезапрашивает
 * список обычным запросом, и права проверяются как всегда.
 */
export interface ServerNotification {
  id: string;
  kind: NotificationKind;
  title: string;
  message: string | null;
  /** Какая сущность изменилась — то же имя, что в системном журнале. */
  entity: string | null;
  entityId: string | null;
  at: string;
}

/** Состояние подключения к хабу; показывается индикатором в шапке. */
export type RealtimeStatus = 'offline' | 'connecting' | 'online' | 'reconnecting';

/** Имя метода, который сервер вызывает у браузера (INotificationClient.Notify). */
export const NOTIFY_METHOD = 'Notify';

/** Адрес хаба относительно <base href>; тот же путь задан в NotificationHub.Route. */
export const HUB_PATH = 'hubs/notifications';
