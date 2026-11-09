namespace TreasuryPortal.Web.Spa;

/// <summary>
/// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development): на сервере не используется.
///
/// Секция Spa:DevServer задана только в appsettings.Development.json. Если Url не задан,
/// приложение отдаёт собранный клиент из wwwroot — так оно работает на IIS.
/// </summary>
public sealed class SpaDevServerOptions
{
    public const string SectionName = "Spa:DevServer";

    /// <summary>Адрес ng serve: на него проксируется всё, что не API.</summary>
    public string? Url { get; set; }

    /// <summary>Поднять ng serve вместе с приложением, если порт свободен.</summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>Папка Angular-клиента относительно папки проекта.</summary>
    public string SourcePath { get; set; } = "../TreasuryPortal.Client";

    public string Command { get; set; } = "npm start";

    /// <summary>
    /// Сколько ждать первой сборки. Должно быть меньше startupTimeLimit модуля IIS (120 с) —
    /// иначе профиль IIS Express упадёт с 500.37.
    /// </summary>
    public int StartTimeoutSeconds { get; set; } = 90;
}
