namespace TreasuryPortal.Integrations;

/// <summary>
/// Внешний сервис не настроен, не ответил или ответил ошибкой. Message — текст для пользователя;
/// подробности (код ответа, SOAP Fault, начало тела ответа) лежат во вложенном исключении
/// и попадают в лог сервера и в системный журнал, но не на экран.
/// </summary>
public sealed class ExternalServiceException(
    string service, string message, int statusCode, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Имя сервиса — раздел Configuration:ServiceLinks.</summary>
    public string Service { get; } = service;

    /// <summary>
    /// Чем ответит портал: 502 — сервис ответил ошибкой или до него не достучаться, 504 — не
    /// ответил вовремя, 503 — сервис не настроен, 404 — сервис сообщил, что такой записи нет.
    /// </summary>
    public int StatusCode { get; } = statusCode;
}

/// <summary>Сервис ответил HTTP-ошибкой. Начало тела ответа нужно логу, пользователю его не показывают.</summary>
public sealed class ExternalResponseException(int statusCode, string body)
    : Exception(body.Length == 0 ? $"HTTP {statusCode}" : $"HTTP {statusCode}: {body}")
{
    public int ResponseStatusCode { get; } = statusCode;

    public string Body { get; } = body;
}
