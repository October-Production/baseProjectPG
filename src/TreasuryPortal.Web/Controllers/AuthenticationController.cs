using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Mvc;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.UOW.Abstract;
using TreasuryPortal.Web.Auth;
using TreasuryPortal.Web.Controllers.Base;

namespace TreasuryPortal.Web.Controllers;

[Route("api/auth")]
public sealed class AuthenticationController(ITreasuryUow uow, CurrentUser currentUser)
    : ControllerCore(uow, currentUser)
{
    /// <summary>
    /// GET api/auth/me — кто вошёл, какие у него роли и права.
    ///
    /// Этот же запрос служит прогревом NTLM-гейта на клиенте, поэтому права на него не нужны:
    /// доступен любому доменному пользователю (fallback-политика), даже без ролей — чтобы
    /// клиент показал «нет доступа», а не ошибку.
    /// </summary>
    [HttpGet("me")]
    public CurrentUserResponse GetMe() => CurrentUserResponse.From(HttpContext.User, Me);
}

/// <summary>
/// Всё, что клиенту нужно знать, чтобы решить, что показывать. Запрашивается один раз при
/// старте приложения — отдельных проверок «можно ли мне X» по ходу работы нет.
/// </summary>
/// <param name="DevAccess">
/// true — роли и права подменены секцией Auth:Dev. Бывает только при отладке (Development);
/// по этому признаку клиент показывает метку DEV в шапке.
/// </param>
public sealed record CurrentUserResponse(
    string Name,
    string Login,
    string? Domain,
    string? FullName,
    string AuthenticationType,
    string[] Roles,
    string[] Permissions,
    bool DevAccess)
{
    public static CurrentUserResponse From(ClaimsPrincipal user, IUserCredential credential)
    {
        var name = user.Identity?.Name ?? credential.Name;
        var slash = name.IndexOf('\\');

        return new CurrentUserResponse(
            Name: name,
            Login: slash >= 0 ? name[(slash + 1)..] : name,
            Domain: slash >= 0 ? name[..slash] : null,
            FullName: credential.FullName,
            AuthenticationType: GetAuthenticationType(user),
            Roles: user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray(),
            Permissions: user.FindAll(AppClaimTypes.Permission).Select(claim => claim.Value).Distinct().ToArray(),
            DevAccess: user.HasClaim(AppClaimTypes.AccessSource, "dev"));
    }

    /// <summary>
    /// IIS отдаёт имя провайдера («Negotiate»), а не протокол, которым тот закончился. Настоящий
    /// пакет — Kerberos или NTLM — знает LSA: у копии токена без явного типа WindowsIdentity
    /// спрашивает его сама.
    /// </summary>
    private static string GetAuthenticationType(ClaimsPrincipal user)
    {
        if (OperatingSystem.IsWindows() && user.Identities.OfType<WindowsIdentity>().FirstOrDefault() is { } windows)
        {
            try
            {
                using var copy = new WindowsIdentity(windows.Token);
                return copy.AuthenticationType ?? string.Empty;
            }
            catch (Exception)
            {
                return windows.AuthenticationType ?? string.Empty;
            }
        }

        // Не Windows: схема исходной идентичности (наша, с ролями, идёт первой)
        return user.Identities
            .FirstOrDefault(identity => identity.AuthenticationType != AccessClaimsTransformation.IdentityType)
            ?.AuthenticationType ?? string.Empty;
    }
}
