using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TreasuryPortal.Integrations.Abstract;
using TreasuryPortal.Integrations.Services.B2;
using TreasuryPortal.Integrations.Services.B2.Abstract;

namespace TreasuryPortal.Integrations;

public static class IntegrationsServiceCollectionExtensions
{
    /// <summary>Больше этого ответ внешнего сервиса не читается: защита памяти от ошибки на той стороне.</summary>
    private const long MaxResponseBytes = 32 * 1024 * 1024;

    /// <summary>
    /// Внешние сервисы одной строкой: клиенты SOAP и REST и сами сервисы. Вызывается после
    /// AddDbCore — системный журнал клиенты берут у слоя данных.
    /// </summary>
    public static IServiceCollection AddIntegrations(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IExternalClients, ExternalClients>();

        // Внешние сервисы: одна строка на сервис, имя — раздел Configuration:ServiceLinks
        services.AddSoapService<IB2Service, B2Service>(configuration, B2Service.Name);

        return services;
    }

    /// <summary>
    /// SOAP-сервис одной строкой: настройки и HttpClient по имени раздела, а в конструктор
    /// сервиса приходит <see cref="Soap.SoapClient"/>, уже привязанный к этому разделу. Сервис
    /// не знает ни имени раздела, ни адреса — только свои операции.
    /// </summary>
    public static IServiceCollection AddSoapService<TService, TImplementation>(
        this IServiceCollection services, IConfiguration configuration, string name)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddExternalService(configuration, name);
        services.TryAddSingleton<TService>(provider => ActivatorUtilities.CreateInstance<TImplementation>(
            provider, provider.GetRequiredService<IExternalClients>().Soap(name)));

        return services;
    }

    /// <summary>То же для REST-сервиса: в конструктор приходит <see cref="Rest.RestClient"/>.</summary>
    public static IServiceCollection AddRestService<TService, TImplementation>(
        this IServiceCollection services, IConfiguration configuration, string name)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddExternalService(configuration, name);
        services.TryAddSingleton<TService>(provider => ActivatorUtilities.CreateInstance<TImplementation>(
            provider, provider.GetRequiredService<IExternalClients>().Rest(name)));

        return services;
    }

    /// <summary>
    /// Только настройки (Configuration:ServiceLinks:&lt;name&gt;) и HttpClient со своим пулом
    /// соединений — для сервиса, который берёт клиент сам через <see cref="IExternalClients"/>.
    /// </summary>
    public static IServiceCollection AddExternalService(
        this IServiceCollection services, IConfiguration configuration, string name)
    {
        services.Configure<ExternalServiceOptions>(name, configuration.GetSection(ExternalServiceOptions.SectionOf(name)));

        services
            .AddHttpClient(name, client =>
            {
                // Сколько ждать, решает каждый вызов по своим настройкам (TimeoutSeconds)
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.MaxResponseContentBufferSize = MaxResponseBytes;
            })
            .ConfigurePrimaryHttpMessageHandler(provider =>
                CreateHandler(provider.GetRequiredService<IOptionsMonitor<ExternalServiceOptions>>().Get(name)));

        return services;
    }

    /// <summary>
    /// Соединение с сервисом. Сертификат сервера проверяется как обычно: если его выдал
    /// внутренний УЦ банка, этот УЦ должен быть в доверенных на сервере портала. Отключать
    /// проверку, как это делал kaznacms, нельзя: тогда за сервис может выдать себя кто угодно в сети.
    /// </summary>
    private static HttpMessageHandler CreateHandler(ExternalServiceOptions options) =>
        new SocketsHttpHandler
        {
            UseProxy = options.UseProxy,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            Credentials = options.Auth == ExternalAuth.Windows ? CredentialCache.DefaultCredentials : null,
        };
}
