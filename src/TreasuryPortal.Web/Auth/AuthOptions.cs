namespace TreasuryPortal.Web.Auth;

/// <summary>
/// Секция Auth в appsettings. В ней осталась только отладочная подмена прав: время кэшей
/// переехало в секцию Configuration (Configuration:UserRolesCacheTimoutSecond,
/// Configuration:HostNameCacheTimoutMinutes), рядом со строкой подключения, — чтобы их можно
/// было выставить в Vault так же, как в BanknoteOps и NBC. Читаются они через ISettingsStore.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Только Development: роли и права текущего пользователя вместо похода в базу.
    /// Удалите секцию, чтобы проверить настоящую выдачу.
    /// </summary>
    public DevAccessOptions? Dev { get; set; }
}

/// <summary>
/// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development): на сервере не используется.
///
/// Подмена выдачи прав, чтобы работать без базы и без доменных групп. В Production секция
/// игнорируется — <see cref="UserDirectory"/> проверяет окружение явно, даже если ключи
/// случайно окажутся в appsettings.json. В шапке портала при подмене горит метка DEV.
/// </summary>
public sealed class DevAccessOptions
{
    public string[] Roles { get; set; } = [];

    public string[] Permissions { get; set; } = [];

    /// <summary>true — выдать все права из <see cref="AppPermissions.All"/>, не перечисляя их.</summary>
    public bool AllPermissions { get; set; }
}
