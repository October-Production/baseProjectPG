namespace TreasuryPortal.Web.Auth;

/// <summary>
/// Веб-роли портала — те же имена, что заведены в базе. Роль отвечает на вопрос «пускать ли
/// пользователя в систему вообще», право — «что именно ему здесь можно».
///
/// Должны совпадать с AppRoles в клиенте (core/auth/access.model.ts).
/// </summary>
public static class AppRoles
{
    /// <summary>Доступ на просмотр: минимум, чтобы войти в портал.</summary>
    public const string Reader = "TPReader";

    /// <summary>Право менять данные.</summary>
    public const string Writer = "TPWriter";

    /// <summary>Администрирование портала.</summary>
    public const string Admin = "TPAdmin";

    /// <summary>Роли, которые спрашиваются у базы при входе. Новая роль добавляется сюда.</summary>
    public static readonly IReadOnlyList<string> All = [Reader, Writer, Admin];
}

/// <summary>
/// Права портала — имена из базы (таблица веб-прав). Список растёт вместе с процессами и
/// сущностями: добавили сущность — добавили её права сюда и в <see cref="All"/>.
///
/// Должны совпадать с AppPermissions в клиенте (core/auth/access.model.ts).
/// </summary>
public static class AppPermissions
{
    /// <summary>Чтение справочников: нужно любому экрану с выпадающими списками.</summary>
    public const string DictionaryRead = "TPDictionaryRead";

    /// <summary>Просмотр журнала операций портала.</summary>
    public const string AuditRead = "TPAuditRead";

    // Пример для сущности: добавляя её, заводите пару Read/Write и вписывайте в All.
    // public const string PaymentOrderRead = "TPPaymentOrderRead";
    // public const string PaymentOrderWrite = "TPPaymentOrderWrite";

    /// <summary>Права, которые спрашиваются у базы при входе.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        DictionaryRead,
        AuditRead,
    ];
}

public static class AppPolicies
{
    /// <summary>Любая роль приложения: доменный пользователь без ролей в API не допускается.</summary>
    public const string User = "User";

    /// <summary>Роль администратора.</summary>
    public const string Admin = "Admin";

    /// <summary>Роль с правом записи.</summary>
    public const string Writer = "Writer";

    /// <summary>Префикс политик, которые создаются на лету под конкретное право.</summary>
    public const string PermissionPrefix = "perm:";

    /// <summary>
    /// Политика «нужно это право». Регистрировать её заранее не нужно: её собирает
    /// <see cref="PermissionPolicyProvider"/> при первом обращении.
    ///
    /// Применение: .RequireAuthorization(AppPolicies.Permission(AppPermissions.DictionaryRead))
    /// </summary>
    public static string Permission(string permission) => PermissionPrefix + permission;
}

public static class AppClaimTypes
{
    /// <summary>Одно право пользователя. Таких claim столько, сколько прав ему выдала база.</summary>
    public const string Permission = "treasury/permission";

    /// <summary>"db" — роли и права пришли из базы, "dev" — подменены секцией Auth:Dev.</summary>
    public const string AccessSource = "treasury/access_source";
}

internal static class AuthHeaders
{
    /// <summary>Метка «этот 400 — сбой handshake»: по ней гейт в Angular перепрогревается и повторяет запрос.</summary>
    public const string Retry = "X-Auth-Retry";
}
