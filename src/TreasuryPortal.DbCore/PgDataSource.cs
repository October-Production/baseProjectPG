using Microsoft.Extensions.Logging;
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
///
/// Строку подключения приносит Vault, и он же может её поменять на ходу (ротация пароля):
/// VaultChangeWatcher перечитывает конфигурацию, а <see cref="Primary"/> на каждом обращении
/// сверяет её с той, на которой построен текущий пул, и при расхождении строит новый.
/// Старый не закрывается сразу — у него могут быть незавершённые команды, — а выводится
/// из обращения и закрывается через <see cref="RetireAfter"/>.
/// </summary>
public sealed class PgDataSource(
    IOptionsMonitor<DbCoreOptions> options,
    ILogger<PgDataSource> logger) : IAsyncDisposable
{
    /// <summary>Сколько дать старому пулу на завершение начатых команд.</summary>
    private static readonly TimeSpan RetireAfter = TimeSpan.FromSeconds(30);

    private readonly Lock _sync = new();

    private volatile Pool? _current;
    private bool _disposed;

    /// <summary>Сессия к мастеру: через неё идут и чтение, и запись.</summary>
    public NpgsqlDataSource Primary
    {
        get
        {
            var connectionString = Configured();
            var pool = _current;

            return pool is not null && pool.ConnectionString == connectionString
                ? pool.Primary
                : Rebuild(connectionString);
        }
    }

    public ValueTask DisposeAsync()
    {
        Pool? pool;
        lock (_sync)
        {
            _disposed = true;
            pool = _current;
            _current = null;
        }

        return pool?.MultiHost.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private string Configured() => options.CurrentValue.ConnectionStrings.PGSQLServer ?? "";

    private NpgsqlDataSource Rebuild(string connectionString)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Пока ждали замок, строку мог построить другой поток
            var previous = _current;
            if (previous is not null && previous.ConnectionString == connectionString)
            {
                return previous.Primary;
            }

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new MessageException(
                    "Не задана строка подключения Configuration:ConnectionStrings:PGSQLServer", 503);
            }

            var multiHost = new NpgsqlDataSourceBuilder(connectionString).BuildMultiHost();
            var pool = new Pool(
                connectionString,
                multiHost,
                multiHost.WithTargetSession(TargetSessionAttributes.Primary));

            _current = pool;

            if (previous is not null)
            {
                logger.LogInformation(
                    "Строка подключения к PostgreSQL изменилась, пул пересоздан. Прежний закроется через {Seconds} с",
                    RetireAfter.TotalSeconds);

                Retire(previous);
            }

            return pool.Primary;
        }
    }

    /// <summary>
    /// Закрывает прежний пул, дав начатым командам доиграть. Закрыть его сразу нельзя:
    /// в этот момент на нём могут выполняться запросы пользователей.
    /// </summary>
    private void Retire(Pool pool) => _ = Task.Run(async () =>
    {
        try
        {
            await Task.Delay(RetireAfter);
            await pool.MultiHost.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось закрыть прежний пул подключений");
        }
    });

    private sealed record Pool(string ConnectionString, NpgsqlMultiHostDataSource MultiHost, NpgsqlDataSource Primary);
}
