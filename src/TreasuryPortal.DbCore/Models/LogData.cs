namespace TreasuryPortal.DbCore.Models;

/// <summary>
/// Одна запись системного журнала — та же форма, что в BanknoteOps: сущность, операция,
/// пользователь и тело запроса в JSON.
///
/// <see cref="LogId"/> заполняет фоновый писатель ответом функции csen.log_event; код запроса
/// его не видит и не ждёт.
/// </summary>
public sealed class LogData
{
    /// <summary>Сущность или процесс — колонка entity.</summary>
    public string Entity { get; init; } = "";

    /// <summary>Что именно делали — колонка op: list, item, save, delete.</summary>
    public string Operation { get; init; } = "";

    /// <summary>DOMAIN\login.</summary>
    public string UserLogin { get; init; } = "";

    /// <summary>Тело запроса в JSON; пишется в jsonb как есть.</summary>
    public string Json { get; set; } = "{}";

    /// <summary>Идентификатор строки журнала, который вернула функция начала операции.</summary>
    public long LogId { get; set; }

    /// <summary>Непустой текст означает, что операция закончилась ошибкой.</summary>
    public string ErrorText { get; set; } = "";
}
