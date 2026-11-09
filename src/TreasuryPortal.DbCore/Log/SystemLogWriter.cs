using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreasuryPortal.DbCore.Models;
using TreasuryPortal.DbCore.Repositories.Abstract;

namespace TreasuryPortal.DbCore.Log;

/// <summary>
/// Фоновая запись системного журнала. В BanknoteOps обе функции журнала вызываются прямо
/// в операции: запрос пользователя ждёт csen.log_event перед работой и csen.log_event_end после.
/// Здесь функции те же, но вызываются отсюда — запрос только кладёт запись в очередь.
///
/// Читатель один, очередь — FIFO, поэтому начало операции всегда обрабатывается раньше её конца.
/// log_id, который вернула функция начала, хранится здесь и подставляется в конец: вызывающая
/// сторона его не видит и не ждёт.
/// </summary>
public sealed class SystemLogWriter(
    LogQueue queue,
    ILogRepository repository,
    IOptions<DbCoreOptions> options,
    ILogger<SystemLogWriter> logger) : BackgroundService
{
    private readonly LogOptions _options = options.Value.Log;

    /// <summary>OperationId → log_id из базы, пока операция не закончилась.</summary>
    private readonly Dictionary<Guid, long> _openOperations = [];

    /// <summary>Порядок появления: по нему вытесняются операции, конец которых потерялся.</summary>
    private readonly Queue<Guid> _openOrder = new();

    private int MaxOpenOperations => Math.Max(64, _options.QueueCapacity);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Системный журнал: фоновая запись запущена");

        // stoppingToken здесь намеренно не используется: цикл заканчивается, когда очередь
        // закрыта в StopAsync, — иначе хвост записей потерялся бы при остановке
        await foreach (var logEvent in queue.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                await HandleAsync(logEvent);
            }
            catch (Exception ex)
            {
                // Журнал — best effort: сбой записи не должен останавливать фоновую службу
                logger.LogError(ex, "Системный журнал: не удалось записать событие {Kind}", logEvent.Kind);
            }
        }

        logger.LogInformation(
            "Системный журнал: очередь дочитана, отброшено за всё время {Dropped} записей",
            queue.DroppedCount);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Закрываем приём и даём писателю дочитать хвост — но не дольше, чем разрешено
        queue.Complete();

        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drain.CancelAfter(TimeSpan.FromSeconds(Math.Max(0, _options.ShutdownDrainSeconds)));

        try
        {
            await base.StopAsync(drain.Token);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Системный журнал: очередь не дописана за отведённое время при остановке");
        }
    }

    private async Task HandleAsync(LogEvent logEvent)
    {
        var data = new LogData
        {
            Entity = logEvent.Entity,
            Operation = logEvent.Operation,
            UserLogin = logEvent.UserLogin,
            Json = logEvent.Json,
            ErrorText = logEvent.ErrorText,
        };

        switch (logEvent.Kind)
        {
            case LogEventKind.OperationStarted:
                await repository.LogStartAsync(data);
                if (data.LogId > 0)
                {
                    Remember(logEvent.OperationId, data.LogId);
                }
                break;

            case LogEventKind.OperationCompleted:
                if (_openOperations.Remove(logEvent.OperationId, out var logId))
                {
                    data.LogId = logId;
                    await repository.LogEndAsync(data);
                }
                break;
        }
    }

    private void Remember(Guid operationId, long logId)
    {
        _openOperations[operationId] = logId;
        _openOrder.Enqueue(operationId);

        // Конец операции мог потеряться вместе с переполненной очередью — не копим такие записи
        while (_openOrder.Count > MaxOpenOperations && _openOrder.TryDequeue(out var oldest))
        {
            _openOperations.Remove(oldest);
        }
    }
}
