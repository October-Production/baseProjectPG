using System.Collections.Concurrent;
using System.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TreasuryPortal.DbCore.Events.Abstract;

namespace TreasuryPortal.DbCore.Events;

/// <summary>
/// Слушает канал PostgreSQL (LISTEN) и передаёт каждое событие получателям
/// <see cref="IDbEventHandler"/>. Так функция базы сообщает порталу о том, что случилось
/// у неё внутри: заявка перешла на следующий шаг, закончился импорт, сработало задание.
///
/// PostgreSQL доставляет событие только после COMMIT транзакции, в которой вызван pg_notify:
/// откатившееся изменение ничего не пришлёт. Кто сделал изменение, неважно — портал, задание
/// по расписанию или другая система.
///
/// Для этого держится одно постоянное подключение к мастеру. Если событий нет
/// <see cref="HeartbeatInterval"/>, служба проверяет подключение запросом и сверяет, не пересоздан
/// ли пул после ротации пароля в Vault. При любом сбое — переподключение с нарастающей паузой;
/// в лог пишется одно предупреждение на обрыв и одна запись о восстановлении.
///
/// События, пришедшие во время обрыва, теряются: LISTEN/NOTIFY их не хранит. Поэтому событие —
/// только повод перечитать данные, а не сами данные.
///
/// Каждый узел портала слушает базу сам и отправляет событие своим подключениям, поэтому общий
/// канал между узлами для этих событий не нужен.
/// </summary>
internal sealed class DbEventListener(
    PgDataSource dataSource,
    IEnumerable<IDbEventHandler> handlers,
    IOptionsMonitor<DbCoreOptions> options,
    ILogger<DbEventListener> logger) : BackgroundService
{
    /// <summary>Сколько ждать события, прежде чем проверить подключение.</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(1);

    private readonly IDbEventHandler[] _handlers = [.. handlers];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_handlers.Length == 0)
        {
            logger.LogDebug("События базы: получателей нет, канал не слушается");
            return;
        }

        // Запуск приложения не ждёт подключения к базе
        await Task.Yield();

        var retryDelay = FirstRetryDelay;
        var failures = 0;
        var disabledLogged = false;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var channel = ConfiguredChannel();
                if (channel is null)
                {
                    if (!disabledLogged)
                    {
                        logger.LogInformation("События базы не слушаются: Configuration:Events:Channel пуст");
                        disabledLogged = true;
                    }

                    await Task.Delay(HeartbeatInterval, stoppingToken);
                    continue;
                }

                disabledLogged = false;

                try
                {
                    await ListenAsync(channel, onListening: () =>
                    {
                        if (failures == 0)
                        {
                            logger.LogInformation("События базы: слушаю канал {Channel}", channel);
                        }
                        else
                        {
                            logger.LogInformation("События базы: подключение восстановлено, слушаю канал {Channel}", channel);
                        }

                        failures = 0;
                        retryDelay = FirstRetryDelay;
                    }, stoppingToken);

                    // Пул пересоздан или в настройках другой канал: переподключаемся сразу
                    continue;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    failures++;
                    if (failures == 1)
                    {
                        logger.LogWarning(ex, "События базы: нет подключения к каналу {Channel}, переподключаюсь в фоне", channel);
                    }
                    else
                    {
                        logger.LogDebug(ex, "События базы: попытка подключения {Attempt} не удалась", failures);
                    }
                }

                await Task.Delay(retryDelay, stoppingToken);
                retryDelay = TimeSpan.FromTicks(Math.Min(retryDelay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Приложение останавливается: подключение закроется вместе со службой
        }
    }

    /// <summary>
    /// Одна сессия прослушивания: подключиться, подписаться и разбирать события, пока не пора
    /// переподключиться. Сбой подключения вылетает исключением наружу.
    /// </summary>
    private async Task ListenAsync(string channel, Action onListening, CancellationToken stoppingToken)
    {
        var source = dataSource.Primary;

        // Событие Notification Npgsql поднимает внутри чтения из сокета: при включённом Keepalive
        // это может случиться и между нашими вызовами, поэтому очередь потокобезопасная
        var received = new ConcurrentQueue<NpgsqlNotificationEventArgs>();

        await using var connection = await source.OpenConnectionAsync(stoppingToken);
        connection.Notification += (_, args) => received.Enqueue(args);

        try
        {
            await RunAsync(connection, $"LISTEN {QuoteIdentifier(channel)}", stoppingToken);
            onListening();

            while (true)
            {
                if (!await connection.WaitAsync(HeartbeatInterval, stoppingToken))
                {
                    // Тишина: без запроса обрыв подключения заметили бы только по следующему событию
                    await RunAsync(connection, "select 1", stoppingToken);
                }

                await DispatchAsync(received, stoppingToken);

                if (Outdated(source, channel))
                {
                    return;
                }
            }
        }
        finally
        {
            await UnlistenAsync(connection);
        }
    }

    private async Task DispatchAsync(
        ConcurrentQueue<NpgsqlNotificationEventArgs> received, CancellationToken stoppingToken)
    {
        while (received.TryDequeue(out var args))
        {
            var dbEvent = new DbEvent(args.Channel, args.Payload);

            foreach (var handler in _handlers)
            {
                try
                {
                    await handler.HandleAsync(dbEvent, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Сбой одного получателя не рвёт подключение и не лишает событие остальных
                    logger.LogError(
                        ex,
                        "События базы: {Handler} не обработал событие из канала {Channel}",
                        handler.GetType().Name,
                        dbEvent.Channel);
                }
            }
        }
    }

    /// <summary>
    /// Пора ли переподключиться: после ротации пароля пул уже другой, а старый скоро закроется,
    /// или в настройках сменили канал.
    /// </summary>
    private bool Outdated(NpgsqlDataSource source, string channel)
    {
        if (!ReferenceEquals(dataSource.Primary, source))
        {
            logger.LogInformation("События базы: пул подключений пересоздан, переподключаюсь");
            return true;
        }

        if (ConfiguredChannel() != channel)
        {
            logger.LogInformation("События базы: в настройках другой канал, переподключаюсь");
            return true;
        }

        return false;
    }

    private string? ConfiguredChannel()
    {
        var channel = options.CurrentValue.Events?.Channel?.Trim();
        return string.IsNullOrEmpty(channel) ? null : channel;
    }

    private static async Task RunAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Снимает подписку, прежде чем подключение вернётся в пул. Иначе оно продолжило бы получать
    /// события, которые никто не читает, и копило бы их, пока его не возьмёт следующий запрос.
    /// </summary>
    private static async Task UnlistenAsync(NpgsqlConnection connection)
    {
        if (connection.State != ConnectionState.Open)
        {
            return;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "UNLISTEN *";
            command.CommandTimeout = 5;
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // Подключение уже сломано: пул его отбросит, и подписка уйдёт вместе с ним
        }
    }

    /// <summary>
    /// Имя канала в кавычках: так LISTEN не приводит его к нижнему регистру, и оно точно
    /// совпадает со строкой, которую функция передаёт в pg_notify.
    /// </summary>
    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";
}
