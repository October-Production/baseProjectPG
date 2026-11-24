using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.Integrations.Abstract;
using TreasuryPortal.Integrations.Rest;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations;

/// <inheritdoc cref="IExternalClients"/>
/// <remarks>
/// Лог у каждого сервиса свой — TreasuryPortal.Integrations.Soap.&lt;Сервис&gt; или .Rest.&lt;Сервис&gt;,
/// так подробности одного сервиса включаются в Logging:LogLevel, не трогая остальные.
/// </remarks>
internal sealed class ExternalClients(
    IHttpClientFactory httpClients,
    IOptionsMonitor<ExternalServiceOptions> options,
    ISystemLog log,
    ILoggerFactory loggers) : IExternalClients
{
    public SoapClient Soap(string service) => new(Caller(service, ExternalAuth.WsSecurity, "Soap"));

    public RestClient Rest(string service) => new(Caller(service, ExternalAuth.Basic, "Rest"));

    private ExternalCaller Caller(string service, ExternalAuth authWithLogin, string kind) =>
        new(
            service,
            authWithLogin,
            httpClients,
            options,
            log,
            loggers.CreateLogger($"TreasuryPortal.Integrations.{kind}.{service}"));
}
