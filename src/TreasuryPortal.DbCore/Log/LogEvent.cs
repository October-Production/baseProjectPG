namespace TreasuryPortal.DbCore.Log;

public enum LogEventKind
{
    /// <summary>Операция началась: csen.log_event(...).</summary>
    OperationStarted,

    /// <summary>Операция закончилась: csen.log_event_end(...).</summary>
    OperationCompleted,
}

/// <summary>
/// Одна запись журнала, уже готовая к отправке в базу: тело запроса сериализовано на стороне
/// вызывающего потока, а фоновому писателю остаётся только вызов функции.
///
/// Начало и конец связаны <see cref="OperationId"/>, а не идентификатором строки журнала:
/// запрос не ждёт ответа функции начала, поэтому log_id известен только фону.
/// </summary>
public sealed record LogEvent
{
    public required LogEventKind Kind { get; init; }

    /// <summary>Сквозной идентификатор операции: связывает начало и конец.</summary>
    public required Guid OperationId { get; init; }

    public string Entity { get; init; } = "";

    public string Operation { get; init; } = "";

    public string UserLogin { get; init; } = "";

    /// <summary>Тело запроса в JSON.</summary>
    public string Json { get; init; } = "{}";

    /// <summary>Для OperationCompleted: непустой текст означает, что операция упала.</summary>
    public string ErrorText { get; init; } = "";

    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
}
