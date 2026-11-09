using Microsoft.Extensions.Options;
using Npgsql;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore;

/// <summary>
/// Источник подключений к PostgreSQL, один на приложение.
///
/// Строится как в BanknoteOps: multi-host, работа идёт через сессию с признаком Primary —
/// на кластере это гарантирует, что чтение и запись попадают на мастер, а на одиночном
/// сервере ведёт себя как обычный пул.
///
/// Создаётся лениво, при первом обращении: без строки подключения приложение всё равно
/// поднимется и покажет понятную ошибку на первом запросе к данным, а не упадёт на старте.
/// Это нужно для локальной отладки интерфейса с подменой прав (Auth:Dev).
/// </summary>
public sealed class PgDataSource : IAsyncDisposable
{
    private readonly Lazy<NpgsqlMultiHostDataSource> _multiHost;
    private readonly Lazy<NpgsqlDataSource> _primary;

    public PgDataSource(IOptions<DbCoreOptions> options)
    {
        var connectionString = options.Value.ConnectionStrings.PGSQLServer;

        _multiHost = new Lazy<NpgsqlMultiHostDataSource>(
            () =>
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new MessageException(
                        "Не задана строка подключения Configuration:ConnectionStrings:PGSQLServer", 503);
                }

                return new NpgsqlDataSourceBuilder(connectionString).BuildMultiHost();
            },
            LazyThreadSafetyMode.ExecutionAndPublication);

        _primary = new Lazy<NpgsqlDataSource>(
            () => _multiHost.Value.WithTargetSession(TargetSessionAttributes.Primary),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Сессия к мастеру: через неё идут и чтение, и запись.</summary>
    public NpgsqlDataSource Primary => _primary.Value;

    public async ValueTask DisposeAsync()
    {
        if (_multiHost.IsValueCreated)
        {
            await _multiHost.Value.DisposeAsync();
        }
    }
}
