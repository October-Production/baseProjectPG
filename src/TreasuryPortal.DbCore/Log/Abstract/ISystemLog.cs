using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.DbCore.Log.Abstract;

/// <summary>
/// Системный журнал так, как его видит репозиторий. Ни один метод не ходит в базу: они кладут
/// запись в очередь, а пишет её фоновый <see cref="SystemLogWriter"/>.
///
/// Использование в репозитории:
///   using var logged = _log.Start(user, "PaymentOrder", "list", request);
///   ...
///   logged.Fail(ex);   // при ошибке
/// Закрытие using отправляет в журнал конец операции.
/// </summary>
public interface ISystemLog
{
    /// <summary>Чтение: запись ставится в очередь, вызывающий поток не ждёт ничего.</summary>
    /// <param name="request">Тело запроса; будет сериализовано в JSON прямо здесь.</param>
    LogOperation Start(IUserCredential user, string entity, string operation, object? request = null);

    /// <summary>
    /// Запись: то же самое плюс общий на процесс замок записи. Ожидание — только на семафоре
    /// в памяти, в базу по-прежнему никто не ходит. Если замок не взят, первый же
    /// <see cref="LogOperation.EnsureWriteLock"/> бросит понятную пользователю ошибку.
    /// </summary>
    ValueTask<LogOperation> StartWriteAsync(
        IUserCredential user,
        string entity,
        string operation,
        object? request = null,
        CancellationToken cancellationToken = default);
}
