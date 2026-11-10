using System.Collections.Concurrent;
using System.Net;
using TreasuryPortal.Web.Configuration.Abstract;

namespace TreasuryPortal.Web.Infrastructure;

/// <summary>
/// В системный журнал пишется машина, с которой пришло изменение. Обратный DNS медленный, а
/// адресов за день повторяется немного, поэтому ответы кэшируются.
///
/// Время кэша (Configuration:HostNameCacheTimoutMinutes) читается при каждой проверке,
/// а не один раз при старте, — новое значение из Vault действует сразу.
/// </summary>
public sealed class ClientHostResolver(ISettingsHolder settings)
{
    private readonly ConcurrentDictionary<IPAddress, CacheItem> _cache = new();

    public string Resolve(ConnectionInfo? connection) => Resolve(connection?.RemoteIpAddress);

    public string Resolve(IPAddress? ip)
    {
        if (ip is null)
        {
            return string.Empty;
        }

        var now = DateTime.UtcNow;
        var lifetime = TimeSpan.FromMinutes(settings.Store.HostNameCacheMinutes);
        var item = _cache.GetOrAdd(ip, _ => new CacheItem());

        if (item.Name is null || now > item.ResolvedAt + lifetime)
        {
            lock (item)
            {
                if (item.Name is null || now > item.ResolvedAt + lifetime)
                {
                    item.Name = Lookup(ip);
                    item.ResolvedAt = now;
                }
            }
        }

        return item.Name;
    }

    private static string Lookup(IPAddress ip)
    {
        try
        {
            var entry = Dns.GetHostEntry(ip);
            if (!string.IsNullOrEmpty(entry.HostName))
            {
                return $"({ip}){entry.HostName}";
            }
        }
        catch (Exception)
        {
            // Машина без обратной записи в DNS — обычное дело, хватит и адреса
        }

        return ip.ToString();
    }

    private sealed class CacheItem
    {
        public string? Name;
        public DateTime ResolvedAt;
    }
}
