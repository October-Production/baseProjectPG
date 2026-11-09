namespace TreasuryPortal.Web.Hubs;

/// <summary>Вид сообщения: по нему клиент выбирает цвет и значок.</summary>
public enum NotificationKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// Сообщение, которое сервер шлёт в браузер. Намеренно маленькое: в нём нет данных сущности,
/// только повод их перечитать.
///
/// Так сделано потому, что получатель мог не иметь права видеть изменившуюся запись. Клиент,
/// получив сообщение, перезапрашивает список обычным запросом — и права проверяются как всегда.
/// </summary>
/// <param name="Id">Идентификатор сообщения: по нему клиент отсеивает повторы после переподключения.</param>
/// <param name="Kind">Вид сообщения.</param>
/// <param name="Title">Короткий заголовок.</param>
/// <param name="Message">Текст для пользователя; может быть пустым.</param>
/// <param name="Entity">Какая сущность изменилась — то же имя, что в системном журнале.</param>
/// <param name="EntityId">Какая запись изменилась, если известно.</param>
/// <param name="At">Когда это произошло.</param>
public sealed record Notification(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string? Message,
    string? Entity,
    string? EntityId,
    DateTimeOffset At)
{
    public static Notification Create(
        NotificationKind kind,
        string title,
        string? message = null,
        string? entity = null,
        string? entityId = null) =>
        new(Guid.NewGuid(), kind, title, message, entity, entityId, DateTimeOffset.Now);
}
