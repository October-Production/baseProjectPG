using TreasuryPortal.DbCore.Models;

namespace TreasuryPortal.DbCore.Repositories.Abstract;

/// <summary>
/// Функции системного журнала. Вызывает их только <see cref="Log.SystemLogWriter"/> —
/// из кода запросов сюда обращаться не нужно, для этого есть <see cref="Log.Abstract.ISystemLog"/>.
/// </summary>
public interface ILogRepository
{
    /// <summary>csen.log_event(...): заполняет <see cref="LogData.LogId"/>.</summary>
    Task LogStartAsync(LogData data);

    /// <summary>csen.log_event_end(...): ничего не делает, если log_id не получен.</summary>
    Task LogEndAsync(LogData data);
}
