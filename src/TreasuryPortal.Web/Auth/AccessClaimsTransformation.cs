using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace TreasuryPortal.Web.Auth;

/// <summary>
/// Превращает доменного пользователя в пользователя портала: к Windows-идентичности добавляется
/// вторая, с ролями и правами из базы.
///
/// Роли лежат в отдельном ClaimsIdentity, который идёт первым, а принципал становится обычным
/// ClaimsPrincipal: так IsInRole("TPAdmin") не ходит в LSA и не путает роль приложения с
/// одноимённой группой Windows. WindowsIdentity остаётся вторым — для имперсонации и SID.
///
/// Вызывается на каждый AuthenticateAsync, иногда дважды за запрос, поэтому идемпотентен,
/// а поход в базу спрятан за кэшем в <see cref="UserDirectory"/>.
/// </summary>
public sealed class AccessClaimsTransformation(
    UserDirectory directory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AccessClaimsTransformation> logger) : IClaimsTransformation
{
    public const string IdentityType = "TreasuryPortal";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true } identity ||
            principal.Identities.Any(i => i.AuthenticationType == IdentityType))
        {
            return principal;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var credential = directory.GetCredential(principal, httpContext);

        AccessResult access;
        try
        {
            access = await directory.GetAccessAsync(credential, httpContext?.RequestAborted ?? default);
        }
        catch (Exception ex)
        {
            // База недоступна — пользователь входит без ролей и увидит «нет доступа»,
            // а не ошибку 500 на каждой странице
            logger.LogWarning(ex, "Выдача прав недоступна, пользователь {User} входит без ролей", credential.Name);
            access = new AccessResult([], [], FromDev: false);
        }

        var name = identity.Name ?? credential.Name;
        var appIdentity = new ClaimsIdentity(IdentityType, ClaimTypes.Name, ClaimTypes.Role);
        appIdentity.AddClaim(new Claim(ClaimTypes.Name, name));
        // NameIdentifier нужен, например, SignalR для Clients.User(...): Windows-аутентификация его не даёт
        appIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, name));
        appIdentity.AddClaim(new Claim(AppClaimTypes.AccessSource, access.FromDev ? "dev" : "db"));

        foreach (var role in access.Roles)
        {
            appIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in access.Permissions)
        {
            appIdentity.AddClaim(new Claim(AppClaimTypes.Permission, permission));
        }

        var result = new ClaimsPrincipal(appIdentity);
        result.AddIdentities(principal.Identities);
        return result;
    }
}
