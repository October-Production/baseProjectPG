using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Log;

/// <inheritdoc cref="ISystemLog"/>
public sealed class SystemLog(ILogSink sink, WriteLock writeLock) : ISystemLog
{
    public LogOperation Start(IUserCredential user, string entity, string operation, object? request = null) =>
        Begin(user, entity, operation, request, lockAcquired: false, useWriteLock: false);

    public async ValueTask<LogOperation> StartWriteAsync(
        IUserCredential user,
        string entity,
        string operation,
        object? request = null,
        CancellationToken cancellationToken = default)
    {
        // Ожидание здесь — только семафор в памяти; в базу по-прежнему никто не ходит
        var acquired = await writeLock.AcquireAsync(cancellationToken);
        return Begin(user, entity, operation, request, acquired, useWriteLock: true);
    }

    private LogOperation Begin(
        IUserCredential user,
        string entity,
        string operation,
        object? request,
        bool lockAcquired,
        bool useWriteLock)
    {
        var id = Guid.NewGuid();
        var login = user?.Name ?? "";
        var json = Serialize(request);

        sink.TryWrite(new LogEvent
        {
            Kind = LogEventKind.OperationStarted,
            OperationId = id,
            Entity = entity,
            Operation = operation,
            UserLogin = login,
            Json = json,
        });

        return new LogOperation(sink, id, entity, operation, login, json, useWriteLock ? writeLock : null, lockAcquired);
    }

    /// <summary>
    /// Тело запроса сериализуется здесь, на потоке запроса: фоновому писателю достаётся готовая
    /// строка, и он не держит ссылку на объект, который к тому времени мог измениться.
    /// </summary>
    private static string Serialize(object? request)
    {
        if (request is null)
        {
            return "{}";
        }

        if (request is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "{}" : text;
        }

        try
        {
            return request.ToJsonString();
        }
        catch (Exception)
        {
            // Журнал не должен ронять операцию из-за того, что аргумент не сериализуется
            return "{}";
        }
    }
}
