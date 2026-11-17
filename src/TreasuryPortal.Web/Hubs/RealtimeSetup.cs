using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TreasuryPortal.DbCore.Events.Abstract;
using TreasuryPortal.Web.Auth;

namespace TreasuryPortal.Web.Hubs;

public static class RealtimeSetup
{
    /// <summary>
    /// Подключение к браузеру в реальном времени: хаб, способ в него писать и мост, по которому
    /// в браузер приходят события из базы.
    /// </summary>
    public static IServiceCollection AddTreasuryRealtime(this IServiceCollection services)
    {
        services
            .AddSignalR(options =>
            {
                // Текст исключения хаба наружу не уходит: ошибку увидит только лог сервера
                options.EnableDetailedErrors = false;
            })
            .AddJsonProtocol(options =>
            {
                // Вид сообщения уходит строкой ("info"), а не числом: так его читает TypeScript
                options.PayloadSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        services.AddSingleton<INotificationPublisher, NotificationPublisher>();

        // События из базы (pg_notify) уходят в браузер через тот же публикатор.
        // Канал слушает DbEventListener из слоя данных — он запускается, увидев этого получателя
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbEventHandler, DbEventBridge>());

        return services;
    }

    /// <summary>
    /// Адрес хаба. Он лежит вне /api, поэтому при отладке исключён из проксирования
    /// на ng serve явно — см. предикат в Spa/SpaHosting.cs.
    /// </summary>
    public static IEndpointRouteBuilder MapTreasuryHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<NotificationHub>(NotificationHub.Route).RequireAuthorization(AppPolicies.User);
        return endpoints;
    }
}
