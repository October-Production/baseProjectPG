using TreasuryPortal.DbCore.Models;
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.DbCore.Repositories.Abstract;

/// <summary>
/// Веб-роли и права пользователя. Источник — база: функция получает пользователя вместе с его
/// AD-группами и возвращает то, что ему выдано.
///
/// В отличие от версии под MS SQL, приложение не перебирает права по одному и не кэширует
/// справочник «право → группы»: PostgreSQL отдаёт сразу весь список.
/// </summary>
public interface IAuthorizationRepository
{
    /// <summary>csen.get_user_roles(user): роли, выданные пользователю.</summary>
    Task<List<string>> GetRolesAsync(IUserCredential user, CancellationToken cancellationToken = default);

    /// <summary>csen.get_user_permissions(user): права, выданные пользователю.</summary>
    Task<List<string>> GetPermissionsAsync(IUserCredential user, CancellationToken cancellationToken = default);

    /// <summary>Роли и права одним вызовом: сначала роли, затем права — права могут от них зависеть.</summary>
    Task<UserAccess> GetUserAccessAsync(IUserCredential user, CancellationToken cancellationToken = default);
}
