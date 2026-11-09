using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TreasuryPortal.Web.Auth;

namespace TreasuryPortal.Web.Hubs;

/// <summary>Что сервер умеет вызывать у браузера. Имена методов видит клиент в connection.on(...).</summary>
public interface INotificationClient
{
    Task Notify(Notification notification);
}

/// <summary>
/// Единственный хаб портала. Отдельные хабы под каждую сущность заводить не нужно: адресация
/// идёт группами и пользователями, а что именно изменилось — написано в самом сообщении.
///
/// Подключение проходит ту же доменную аутентификацию, что и остальной портал, и ту же политику:
/// без роли портала в хаб не пускают. Пользователь, уже подключённый к хабу, остаётся с правами,
/// которые были у него в момент подключения, — права меняются редко, а переподключение случается
/// само при любом обрыве.
/// </summary>
[Authorize(AppPolicies.User)]
public sealed class NotificationHub(ILogger<NotificationHub> logger) : Hub<INotificationClient>
{
    /// <summary>Адрес хаба. Тот же путь прописан в клиенте (core/realtime/realtime.service.ts).</summary>
    public const string Route = "/hubs/notifications";

    public override async Task OnConnectedAsync()
    {
        // Группы по ролям: так сервер может позвать «всех казначеев с правом записи»,
        // не перечисляя их поимённо. Из групп SignalR убирает сам при отключении.
        var roles = Context.User?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct() ?? [];
        foreach (var role in roles)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role));
        }

        logger.LogDebug(
            "SignalR: подключился {User} ({ConnectionId})",
            Context.User?.Identity?.Name,
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            logger.LogDebug(
                exception,
                "SignalR: обрыв у {User} ({ConnectionId})",
                Context.User?.Identity?.Name,
                Context.ConnectionId);
        }

        return base.OnDisconnectedAsync(exception);
    }

    internal static string RoleGroup(string role) => $"role:{role}";
}
