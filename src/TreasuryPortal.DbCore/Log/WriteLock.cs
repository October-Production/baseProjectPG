using Microsoft.Extensions.Options;

namespace TreasuryPortal.DbCore.Log;

/// <summary>
/// Общий на процесс замок записи: две правки одновременно в одну сущность не идут.
///
/// Внимание: замок живёт в памяти приложения, поэтому защищает только один процесс. На ферме
/// из нескольких узлов IIS его нужно заменить на блокировку в базе (pg_advisory_lock) —
/// иначе узлы друг о друге не знают. Отключается ключом Configuration:WriteLockWaitSeconds = 0.
/// </summary>
public sealed class WriteLock(IOptions<DbCoreOptions> options) : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly int _waitSeconds = options.Value.WriteLockWaitSeconds;

    /// <summary>true — замок не используется вовсе (WriteLockWaitSeconds = 0).</summary>
    public bool Disabled => _waitSeconds <= 0;

    /// <returns>false — замок занят дольше разрешённого ожидания.</returns>
    public async ValueTask<bool> AcquireAsync(CancellationToken cancellationToken = default)
    {
        if (Disabled)
        {
            return true;
        }

        return await _semaphore.WaitAsync(TimeSpan.FromSeconds(_waitSeconds), cancellationToken);
    }

    public void Release()
    {
        if (!Disabled)
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
