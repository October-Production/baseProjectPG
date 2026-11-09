using Microsoft.AspNetCore.Authorization;

namespace TreasuryPortal.Web.Auth;

/// <summary>
/// Закрывает контроллер или действие правом портала:
///
///   [AuthorizePerm(AppPermissions.DictionaryRead)]
///
/// Атрибут нужен потому, что имя политики собирается методом
/// <see cref="AppPolicies.Permission"/>, а в аргумент атрибута можно положить только константу.
/// Здесь имя собирается в конструкторе, и в коде остаётся само право, а не строка политики.
///
/// В отличие от AuthorizePerm из NBC, этот атрибут сам в базу не ходит: права уже лежат
/// в claim'ах, их положил <see cref="AccessClaimsTransformation"/> при входе. Проверку делает
/// <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuthorizePermAttribute(string permission) : AuthorizeAttribute(AppPolicies.Permission(permission))
{
    /// <summary>Право, которым закрыто действие — пригодится в логах и в тестах.</summary>
    public string Permission { get; } = permission;
}
