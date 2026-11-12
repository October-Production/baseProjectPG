using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Log;

/// <inheritdoc cref="ISystemLog"/>
public sealed class SystemLog(ILogSink sink) : ISystemLog
{
    public LogOperation Start(IUserCredential user, string entity, string operation, object? request = null)
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

        return new LogOperation(sink, id, entity, operation, login, json);
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
