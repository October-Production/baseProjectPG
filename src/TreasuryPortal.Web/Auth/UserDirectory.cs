using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TreasuryPortal.DbCore.Models;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.UOW.Abstract;
using TreasuryPortal.Web.Infrastructure;

namespace TreasuryPortal.Web.Auth;

/// <summary>
/// Кто вошёл и что ему можно. Здесь сходятся два дорогих источника — AD (имена групп) и база
/// (веб-роли и права), — и оба кэшируются: экран открывает залп запросов, и платить за каждый
/// обращением к домену и к базе нельзя.
/// </summary>
public sealed class UserDirectory(
    ITreasuryUow uow,
    IMemoryCache cache,
    ClientHostResolver hostResolver,
    IOptionsMonitor<AuthOptions> options,
    IHostEnvironment environment,
    ILogger<UserDirectory> logger)
{
    /// <summary>Учётные данные запроса: имя, домен, машина и имена AD-групп.</summary>
    public IUserCredential GetCredential(ClaimsPrincipal principal, HttpContext? httpContext)
    {
        var host = hostResolver.Resolve(httpContext?.Connection);
        var name = principal.Identity?.Name ?? "";
        var key = $"credential:{name}|{host}";

        if (cache.TryGetValue<IUserCredential>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        // Внутри AdHelper имена групп и профиль уже кэшируются на весь процесс,
        // здесь кэшируется сам готовый объект — чтобы не собирать его на каждый запрос
        var credential = new UserCredential(principal, host);
        return Cache(key, credential);
    }

    /// <summary>Роли и права пользователя. Один поход в базу на пользователя за окно кэша.</summary>
    public Task<AccessResult> GetAccessAsync(IUserCredential user, CancellationToken cancellationToken = default)
    {
        if (GetDevAccess() is { } dev)
        {
            return Task.FromResult(dev);
        }

        var key = $"access:{user.Name}";
        if (cache.TryGetValue<Task<AccessResult>>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var pending = LoadAccessAsync(user, cancellationToken);

        var seconds = options.CurrentValue.AccessCacheSeconds;
        if (seconds > 0)
        {
            cache.Set(key, pending, TimeSpan.FromSeconds(seconds));

            // Сорванный запрос не должен остаться в кэше: иначе одна заминка закрывает
            // пользователю доступ на всё время жизни записи
            _ = pending.ContinueWith(
                _ => cache.Remove(key),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        return pending;
    }

    private async Task<AccessResult> LoadAccessAsync(IUserCredential user, CancellationToken cancellationToken)
    {
        // Отпускаем вызывающий поток: эта задача кладётся в кэш и её ждут все параллельные запросы
        await Task.Yield();

        try
        {
            // PostgreSQL отдаёт сразу все выданные роли и права — перебирать их по одному,
            // как это делала версия под MS SQL, больше не нужно
            var access = await uow.Authorization.GetUserAccessAsync(user, cancellationToken);

            return new AccessResult(access.Roles, access.Permissions, FromDev: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось получить роли и права пользователя {User}", user.Name);
            throw;
        }
    }

    /// <summary>
    /// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development): подмена выдачи, чтобы работать без базы и без доменных
    /// групп. Вне Development всегда возвращает null, даже если секция Auth:Dev задана.
    /// </summary>
    private AccessResult? GetDevAccess()
    {
        if (!environment.IsDevelopment() || options.CurrentValue.Dev is not { } dev)
        {
            return null;
        }

        var permissions = dev.AllPermissions ? AppPermissions.All : dev.Permissions;
        return new AccessResult(dev.Roles, permissions, FromDev: true);
    }

    private IUserCredential Cache(string key, IUserCredential credential)
    {
        var seconds = options.CurrentValue.AccessCacheSeconds;
        if (seconds > 0)
        {
            cache.Set(key, credential, TimeSpan.FromSeconds(seconds));
        }

        return credential;
    }
}

/// <param name="FromDev">true — выдача подменена секцией Auth:Dev, в базу не ходили.</param>
public sealed record AccessResult(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions, bool FromDev);
