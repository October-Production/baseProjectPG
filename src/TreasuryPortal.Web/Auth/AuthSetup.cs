using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using TreasuryPortal.Web.Infrastructure;

namespace TreasuryPortal.Web.Auth;

public static class AuthSetup
{
    /// <summary>
    /// Доменная аутентификация и выдача прав одной строкой.
    ///
    /// На IIS обработчик Negotiate отдаёт работу Windows-аутентификации самого IIS (DeferToServer),
    /// на Kestrel (dotnet run) проводит handshake сам. Код один и тот же.
    /// </summary>
    public static IServiceCollection AddTreasuryAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<ClientHostResolver>();
        services.AddSingleton<UserDirectory>();
        services.AddScoped<CurrentUser>();

        services
            .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate(negotiate => negotiate.Events = new NegotiateEvents
            {
                OnAuthenticationFailed = MarkHandshakeFailure,
            });

        services.AddSingleton<IClaimsTransformation, AccessClaimsTransformation>();

        // Политики под права собираются на лету: новое право не нужно регистрировать здесь
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            // Всё, что не помечено явно, — только для аутентифицированных: и API, и index.html, и статика
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AppPolicies.User, policy => policy.RequireRole(AppRoles.All))
            .AddPolicy(AppPolicies.Writer, policy => policy.RequireRole(AppRoles.Writer, AppRoles.Admin))
            .AddPolicy(AppPolicies.Admin, policy => policy.RequireRole(AppRoles.Admin));

        return services;
    }

    /// <summary>
    /// Только Kestrel: на IIS handshake делает сам IIS. По умолчанию обработчик отвечает на сбой
    /// handshake голым 400, а на запрос без авторизации посреди handshake — исключением (500).
    /// Метка X-Auth-Retry и единый 400 позволяют гейту в Angular отличить это от ошибок валидации:
    /// перепрогрев и один повтор. Неверные креды (CredentialError) не трогаем — дальше штатный 401.
    /// </summary>
    private static Task MarkHandshakeFailure(AuthenticationFailedContext context)
    {
        context.Response.Headers[AuthHeaders.Retry] = "1";
        if (context.Exception is InvalidOperationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.HandleResponse();
        }

        return Task.CompletedTask;
    }
}
