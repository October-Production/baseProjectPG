using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TreasuryPortal.Web.Auth;
using TreasuryPortal.Web.Hubs;

namespace TreasuryPortal.Web.Controllers;

/// <summary>
/// Рассылка сообщений казначеям. Заодно это образец того, как любая фича шлёт события
/// в браузер: берёт из DI <see cref="INotificationPublisher"/> и вызывает один метод.
/// </summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationController(INotificationPublisher publisher) : ControllerBase
{
    /// <summary>POST api/notifications/broadcast — объявление всем, кто сейчас в портале.</summary>
    [HttpPost("broadcast")]
    [Authorize(Policy = AppPolicies.Admin)]
    public async Task<IActionResult> BroadcastAsync(
        [FromBody] BroadcastRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Вкажіть заголовок повідомлення");
            return ValidationProblem(ModelState);
        }

        var notification = Notification.Create(
            request.Kind ?? NotificationKind.Info,
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Message) ? null : request.Message.Trim());

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            await publisher.ToAllAsync(notification, cancellationToken);
        }
        else
        {
            await publisher.ToRoleAsync(request.Role, notification, cancellationToken);
        }

        return Accepted(notification);
    }
}

/// <param name="Role">Пусто — всем, иначе только держателям этой роли портала.</param>
public sealed record BroadcastRequest(string Title, string? Message, NotificationKind? Kind, string? Role);
