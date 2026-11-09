using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Log;

/// <summary>
/// Одна операция в системном журнале. Создаётся через <see cref="ISystemLog"/>, живёт в
/// using-блоке репозитория: закрытие блока отправляет в журнал конец операции и отпускает
/// замок записи.
///
/// Ни один метод не ждёт базу — всё уходит в очередь.
/// </summary>
public sealed class LogOperation : IDisposable
{
    private readonly ILogSink _sink;
    private readonly WriteLock? _writeLock;
    private readonly bool _lockAcquired;
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
        string json,
        WriteLock? writeLock,
        bool lockAcquired)
    {
        _sink = sink;
        Id = id;
        _entity = entity;
        _operation = operation;
        _userLogin = userLogin;
        _json = json;
        _writeLock = writeLock;
        _lockAcquired = lockAcquired;
    }

    public Guid Id { get; }

    /// <summary>
    /// Бросает понятную пользователю ошибку, если замок записи взять не удалось.
    /// Вызывается первым делом в теле операции записи.
    /// </summary>
    public void EnsureWriteLock()
    {
        if (_writeLock is not null && !_writeLock.Disabled && !_lockAcquired)
        {
            throw new MessageException("Не вдалося отримати блокування запису — спробуйте ще раз", 409);
        }
    }

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

        if (_lockAcquired)
        {
            _writeLock?.Release();
        }

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
