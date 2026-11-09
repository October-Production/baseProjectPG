using Microsoft.AspNetCore.SignalR;

namespace TreasuryPortal.Web.Hubs;

/// <summary>
/// Как отправить сообщение в браузер. Это единственное, что нужно знать коду фичи, — сам хаб
/// и IHubContext в эндпоинтах и службах не используются.
///
/// Применение — в контроллере сущности, после удачного сохранения:
///   await _notify.ToRoleAsync(AppRoles.Reader,
///       Notification.Create(NotificationKind.Info, "Ліміти оновлено", entity: "limit"));
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Всем, кто сейчас в портале.</summary>
    Task ToAllAsync(Notification notification, CancellationToken cancellationToken = default);

    /// <summary>Всем, у кого есть эта роль портала.</summary>
    Task ToRoleAsync(string role, Notification notification, CancellationToken cancellationToken = default);

    /// <summary>Одному пользователю: имя в виде DOMAIN\login, как в системном журнале.</summary>
    Task ToUserAsync(string userName, Notification notification, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="INotificationPublisher"/>
internal sealed class NotificationPublisher(IHubContext<NotificationHub, INotificationClient> hub)
    : INotificationPublisher
{
    public Task ToAllAsync(Notification notification, CancellationToken cancellationToken = default) =>
        hub.Clients.All.Notify(notification);

    public Task ToRoleAsync(string role, Notification notification, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(NotificationHub.RoleGroup(role)).Notify(notification);

    /// <summary>
    /// Адресация по пользователю работает благодаря claim'у NameIdentifier, который
    /// AccessClaimsTransformation кладёт рядом с именем: Windows-аутентификация его не даёт,
    /// а SignalR без него не знает, кому слать.
    /// </summary>
    public Task ToUserAsync(string userName, Notification notification, CancellationToken cancellationToken = default) =>
        hub.Clients.User(userName).Notify(notification);
}
