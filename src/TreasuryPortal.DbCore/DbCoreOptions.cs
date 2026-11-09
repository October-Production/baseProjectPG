namespace TreasuryPortal.DbCore;

/// <summary>Секция Configuration в appsettings: всё, что нужно слою данных.</summary>
public sealed class DbCoreOptions
{
    public const string SectionName = "Configuration";

    /// <summary>Строки подключения. На сервере приходят из Vault.</summary>
    public ConnectionStringsOptions ConnectionStrings { get; set; } = new();

    /// <summary>Системный журнал: очередь и фоновая запись.</summary>
    public LogOptions Log { get; set; } = new();

    /// <summary>
    /// Сколько секунд ждать общий замок записи. Он один на процесс: две параллельные правки
    /// одной сущности не должны идти одновременно. 0 — не брать замок вовсе.
    /// </summary>
    public int WriteLockWaitSeconds { get; set; } = 10;
}

public sealed class ConnectionStringsOptions
{
    /// <summary>PostgreSQL портала. Имя ключа то же, что в BanknoteOps.</summary>
    public string PGSQLServer { get; set; } = "";
}

public sealed class LogOptions
{
    /// <summary>
    /// Сколько записей держать в очереди. Очередь ограничена намеренно: если база недоступна,
    /// приложение должно терять журнал, а не память.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>Сколько секунд ждать дозапись очереди при остановке приложения.</summary>
    public int ShutdownDrainSeconds { get; set; } = 5;
}
