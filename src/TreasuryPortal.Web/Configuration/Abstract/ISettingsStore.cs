namespace TreasuryPortal.Web.Configuration.Abstract;

/// <summary>
/// Типизированные настройки приложения — то же, что ISettingsStore в BanknoteOps и NBC:
/// одно место, где код берёт значения из конфигурации, а значит и из Vault.
///
/// Отличие от BanknoteOps: свойства только для чтения, а не init. Там значения снимаются
/// один раз при старте, и после ротации пароля в Vault приложение до перезапуска ходит
/// со старым. Здесь каждое свойство читается из конфигурации в момент обращения,
/// поэтому новое значение из Vault видно сразу.
///
/// Новая настройка интеграции (почтовый сервис, лимиты файлов, AD-группы согласующих)
/// добавляется сюда одним свойством и одной строкой в <see cref="SettingsStore"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Строка подключения к PostgreSQL. Ключ тот же, что в BanknoteOps.</summary>
    string PGSQLServer { get; }

    /// <summary>
    /// Сколько секунд держать в памяти роли и права пользователя.
    /// Ключ Configuration:UserRolesCacheTimoutSecond — тот же, что в BanknoteOps и NBC.
    /// </summary>
    int AccessCacheSeconds { get; }

    /// <summary>
    /// Сколько минут держать в памяти имя машины клиента для журнала.
    /// Ключ Configuration:HostNameCacheTimoutMinutes — тот же, что в NBC.
    /// </summary>
    int HostNameCacheMinutes { get; }
}
