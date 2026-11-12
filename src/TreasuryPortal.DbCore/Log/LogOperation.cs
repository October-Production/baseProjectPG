using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Log;

/// <summary>
/// Одна операция в системном журнале. Создаётся через <see cref="ISystemLog"/>, живёт в
/// using-блоке репозитория: закрытие блока отправляет в журнал конец операции.
///
/// Ни один метод не ждёт базу — всё уходит в очередь.
/// </summary>
public sealed class LogOperation : IDisposable
{
    private readonly ILogSink _sink;
    private readonly string _entity;
    private readonly string _operation;
    private readonly string _userLogin;
    private readonly string _json;

    private string _errorText = "";
    private bool _disposed;

    internal LogOperation(
        ILogSink sink,
        Guid id,
        string entity,
        string operation,
        string userLogin,
        string json)
    {
        _sink = sink;
        Id = id;
        _entity = entity;
        _operation = operation;
        _userLogin = userLogin;
        _json = json;
    }

    public Guid Id { get; }

    /// <summary>Ошибка в операции: её текст уйдёт в журнал вместе с концом операции.</summary>
    public void Fail(Exception exception)
    {
        var (message, _, _) = Helper.GetExceptionText(exception);
        Fail(message);
    }

    public void Fail(string errorText)
    {
        if (!string.IsNullOrEmpty(errorText))
        {
            _errorText = errorText;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _sink.TryWrite(new LogEvent
        {
            Kind = LogEventKind.OperationCompleted,
            OperationId = Id,
            Entity = _entity,
            Operation = _operation,
            UserLogin = _userLogin,
            Json = _json,
            ErrorText = _errorText,
        });
    }
}
