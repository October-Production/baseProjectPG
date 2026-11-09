using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreasuryPortal.DbCore.Log.Abstract;

namespace TreasuryPortal.DbCore.Log;

/// <summary>
/// Очередь системного журнала между запросом и фоновым писателем.
///
/// Очередь ограничена намеренно: если база недоступна или писатель не успевает, приложение
/// должно терять журнал, а не память и не время пользователя. Поэтому режим DropWrite —
/// запись просто не принимается, вызывающий поток не блокируется никогда.
/// </summary>
public sealed class LogQueue : ILogSink
{
    private readonly Channel<LogEvent> _channel;
    private readonly ILogger<LogQueue> _logger;
    private long _dropped;
    private long _loggedDropMilestone;

    public LogQueue(IOptions<DbCoreOptions> options, ILogger<LogQueue> logger)
    {
        _logger = logger;

        var capacity = Math.Max(16, options.Value.Log.QueueCapacity);
        _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true, // читатель один — SystemLogWriter, порядок событий сохраняется
            SingleWriter = false,
        });
    }

    public long DroppedCount => Interlocked.Read(ref _dropped);

    internal ChannelReader<LogEvent> Reader => _channel.Reader;

    public bool TryWrite(LogEvent logEvent)
    {
        if (_channel.Writer.TryWrite(logEvent))
        {
            return true;
        }

        var dropped = Interlocked.Increment(ref _dropped);

        // Предупреждать о потерях, но не захлёбываться: раз на каждую следующую тысячу
        var milestone = dropped / 1000;
        if (Interlocked.Exchange(ref _loggedDropMilestone, milestone) != milestone)
        {
            _logger.LogWarning(
                "Системный журнал не успевает: отброшено {Dropped} записей. Проверьте доступность базы",
                dropped);
        }

        return false;
    }

    /// <summary>Закрывает приём: вызывается при остановке приложения, чтобы писатель дочитал хвост.</summary>
    internal void Complete() => _channel.Writer.TryComplete();
}
