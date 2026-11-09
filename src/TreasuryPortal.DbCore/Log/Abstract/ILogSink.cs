namespace TreasuryPortal.DbCore.Log.Abstract;

/// <summary>
/// Граница между запросом и журналом. Приём записи обязан быть мгновенным и никогда не ждать
/// базу: если очередь переполнена, запись теряется, а запрос пользователя продолжается.
/// </summary>
public interface ILogSink
{
    /// <returns>false — очередь переполнена и запись отброшена.</returns>
    bool TryWrite(LogEvent logEvent);

    /// <summary>Сколько записей отброшено за всё время работы процесса.</summary>
    long DroppedCount { get; }
}
