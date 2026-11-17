namespace TreasuryPortal.DbCore.Events.Abstract;

/// <summary>
/// Получатель событий базы. Регистрируется в DI, а <see cref="DbEventListener"/> передаёт ему
/// каждое событие из канала. Получателей может быть несколько — событие достаётся всем по очереди.
///
/// Вызов не должен надолго задерживаться: пока получатель работает, следующее событие ждёт.
/// </summary>
public interface IDbEventHandler
{
    Task HandleAsync(DbEvent dbEvent, CancellationToken cancellationToken);
}
