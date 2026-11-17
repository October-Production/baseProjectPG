using System.Text.Json;
using TreasuryPortal.DbCore.Events;
using TreasuryPortal.DbCore.Events.Abstract;

namespace TreasuryPortal.Web.Hubs;

/// <summary>
/// Мост из базы в браузер. Функция базы вызывает pg_notify, слушатель слоя данных передаёт
/// событие сюда, а здесь оно превращается в <see cref="Notification"/> и уходит адресатам
/// через <see cref="INotificationPublisher"/>.
///
/// Так уведомление о переходе заявки по флоу уходит, кто бы этот переход ни сделал:
/// пользователь в портале, задание по расписанию или другая система.
///
/// Формат события описан в db/README.md, раздел 7. Событие без адресата не уходит никому:
/// «всем» нужно указать явно, чтобы ошибка в функции не превратилась в рассылку на весь портал.
/// Неразобранное событие пропускается с предупреждением в лог — слушатель при этом работает дальше.
/// </summary>
internal sealed class DbEventBridge(INotificationPublisher publisher, ILogger<DbEventBridge> logger)
    : IDbEventHandler
{
    /// <summary>Сколько символов события показывать в логе, если его не удалось разобрать.</summary>
    private const int LoggedPayloadLength = 500;

    public async Task HandleAsync(DbEvent dbEvent, CancellationToken cancellationToken)
    {
        if (Parse(dbEvent) is not { } message)
        {
            return;
        }

        // Одно сообщение на всех адресатов: тот, кто попал и в роль, и в список пользователей,
        // получит его дважды, но клиент отсеивает повторы по Id и покажет один раз
        var notification = Notification.Create(
            message.Kind, message.Title, message.Text, message.Entity, message.EntityId);

        if (message.All)
        {
            await publisher.ToAllAsync(notification, cancellationToken);
        }
        else
        {
            foreach (var role in message.Roles)
            {
                await publisher.ToRoleAsync(role, notification, cancellationToken);
            }

            foreach (var user in message.Users)
            {
                await publisher.ToUserAsync(user, notification, cancellationToken);
            }
        }

        logger.LogDebug(
            "Событие базы из канала {Channel} отправлено: всем {All}, роли {Roles}, пользователи {Users}, сущность {Entity}",
            dbEvent.Channel,
            message.All,
            message.Roles,
            message.Users,
            message.Entity);
    }

    private EventMessage? Parse(DbEvent dbEvent)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(dbEvent.Payload);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(
                ex,
                "Событие базы из канала {Channel} пропущено: это не JSON. {Payload}",
                dbEvent.Channel,
                Shorten(dbEvent.Payload));
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                logger.LogWarning(
                    "Событие базы из канала {Channel} пропущено: ожидался JSON-объект. {Payload}",
                    dbEvent.Channel,
                    Shorten(dbEvent.Payload));
                return null;
            }

            var all = root.TryGetProperty("all", out var allValue) && allValue.ValueKind == JsonValueKind.True;
            var roles = Names(root, "roles");
            var users = Names(root, "users");

            if (!all && roles.Count == 0 && users.Count == 0)
            {
                logger.LogWarning(
                    "Событие базы из канала {Channel} пропущено: нет адресата — укажите roles, users или all. {Payload}",
                    dbEvent.Channel,
                    Shorten(dbEvent.Payload));
                return null;
            }

            return new EventMessage(
                all,
                roles,
                users,
                Kind(Text(root, "kind")),
                Text(root, "title") ?? "",
                Text(root, "message"),
                Text(root, "entity"),
                Text(root, "entityId"));
        }
    }

    /// <summary>Строка из поля события. Число тоже принимается: id записи из базы часто приходит числом.</summary>
    private static string? Text(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>Имена адресатов: массив строк или одна строка. Регистр сохраняется — он важен.</summary>
    private static IReadOnlyList<string> Names(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return [];
        }

        IEnumerable<string?> names = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()),
            JsonValueKind.String => [value.GetString()],
            _ => [],
        };

        return names
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Вид сообщения; неизвестное или пустое значение — обычное информационное.</summary>
    private static NotificationKind Kind(string? value) =>
        Enum.TryParse<NotificationKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
            ? kind
            : NotificationKind.Info;

    private static string Shorten(string payload) =>
        payload.Length <= LoggedPayloadLength ? payload : payload[..LoggedPayloadLength] + "…";

    /// <param name="Text">Текст под заголовком (поле message).</param>
    private sealed record EventMessage(
        bool All,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Users,
        NotificationKind Kind,
        string Title,
        string? Text,
        string? Entity,
        string? EntityId);
}
