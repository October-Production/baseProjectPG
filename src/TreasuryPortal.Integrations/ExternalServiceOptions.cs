namespace TreasuryPortal.Integrations;

/// <summary>
/// Как достучаться до одного внешнего сервиса: раздел Configuration:ServiceLinks:&lt;Сервис&gt;.
/// Ключи Endpoint, User и PWD называются так же, как в kaznacms, поэтому значения из Vault
/// переносятся без переименования.
///
/// Читается на каждом вызове (IOptionsMonitor): новый адрес или пароль из Vault действует сразу.
/// Исключение — UseProxy и Auth = Windows: они задают само соединение и подхватываются, когда
/// фабрика пересоздаёт обработчик соединений (раз в две минуты).
/// </summary>
public sealed class ExternalServiceOptions
{
    public const string SectionName = "Configuration:ServiceLinks";

    /// <summary>
    /// Адрес сервиса. Для SOAP — точка вызова целиком; для REST — база, к которой добавляется
    /// путь запроса.
    /// </summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Логин для WS-Security и Basic.</summary>
    public string User { get; set; } = "";

    /// <summary>Пароль для WS-Security и Basic. Хранится только в Vault.</summary>
    public string PWD { get; set; } = "";

    /// <summary>Токен для Bearer. Хранится только в Vault.</summary>
    public string Token { get; set; } = "";

    /// <summary>
    /// Как портал представляется сервису. Не задано — по логину: если логин есть, то WS-Security
    /// для SOAP и Basic для REST (так ходил kaznacms), если нет — без входа.
    /// </summary>
    public ExternalAuth? Auth { get; set; }

    /// <summary>Сколько секунд ждать ответа. Не дождались — пользователь получает 504.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Ходить через системный прокси. По умолчанию нет: внутренние сервисы банка доступны
    /// напрямую. Включайте для сервисов в интернете, если сервер выходит туда через прокси.
    /// </summary>
    public bool UseProxy { get; set; }

    /// <summary>Как назвать сервис в сообщении пользователю. Пусто — имя раздела.</summary>
    public string Title { get; set; } = "";

    /// <summary>Для SOAP: Soap11 (text/xml и заголовок SOAPAction) или Soap12 (application/soap+xml).</summary>
    public SoapVersion SoapVersion { get; set; } = SoapVersion.Soap11;

    /// <summary>
    /// Для SOAP: SOAPAction операций, если шина ждёт не то, что записано в контракте. Ключ — имя
    /// операции (элемент запроса): Actions:getDeals4Treasury.
    /// </summary>
    public Dictionary<string, string> Actions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string SectionOf(string service) => $"{SectionName}:{service}";
}

public enum ExternalAuth
{
    /// <summary>Без входа.</summary>
    None,

    /// <summary>Заголовок Authorization: Basic из User и PWD.</summary>
    Basic,

    /// <summary>Только SOAP: UsernameToken из User и PWD в заголовке конверта.</summary>
    WsSecurity,

    /// <summary>Заголовок Authorization: Bearer из Token.</summary>
    Bearer,

    /// <summary>Учётная запись, под которой работает портал (пул приложений IIS): Kerberos или NTLM.</summary>
    Windows,
}

public enum SoapVersion
{
    Soap11,
    Soap12,
}
