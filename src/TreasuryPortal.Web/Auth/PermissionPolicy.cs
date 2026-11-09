using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace TreasuryPortal.Web.Auth;

/// <summary>Требование «у пользователя есть это право».</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Проверка права. В базу не ходит: права уже лежат в claim'ах, их положил
/// <see cref="AccessClaimsTransformation"/> при входе.
///
/// Так изменилось по сравнению с прежними проектами: раньше AuthorizePermAttribute на каждое
/// действие вызывал хранимую процедуру, и открытие экрана стоило десятков обращений к базе.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(AppClaimTypes.Permission, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Собирает политику «нужно право X» при первом обращении к ней, поэтому новое право не нужно
/// регистрировать в Program.cs — достаточно константы в <see cref="AppPermissions"/> и
/// .RequireAuthorization(AppPolicies.Permission(...)) на эндпоинте.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(AppPolicies.PermissionPrefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permission = policyName[AppPolicies.PermissionPrefix.Length..];

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}
