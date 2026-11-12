using TreasuryPortal.Web.Configuration.Abstract;

namespace TreasuryPortal.Web.Configuration;

/// <inheritdoc cref="ISettingsStore"/>
/// <remarks>
/// Читает ту же конфигурацию, в которую VaultInit() подмешал значения из Vault. Ключи — те же,
/// что используют слой данных (DbCoreOptions) и аутентификация (AuthOptions), поэтому значение
/// у одного ключа всегда одно, с какой бы стороны его ни читали.
/// </remarks>
public sealed class SettingsStore(IConfiguration configuration) : ISettingsStore
{
    public string PGSQLServer => configuration["Configuration:ConnectionStrings:PGSQLServer"] ?? "";

    // Ключи кэшей — те же, что в BanknoteOps и NBC, вместе с написанием «Timout»: это уже
    // имена в Vault, и при совпадении значение подхватится оттуда без заведения нового ключа
    public int AccessCacheSeconds => Int("Configuration:UserRolesCacheTimoutSecond", 60);

    public int HostNameCacheMinutes => Int("Configuration:HostNameCacheTimoutMinutes", 60);

    // Новая настройка интеграции добавляется одной строкой, как в BanknoteOps:
    // public string EmailServiceEndpoint => configuration["Configuration:ServiceLinks:EmailService:Endpoint"] ?? "";
    // public int MaxFileSize => Int("Configuration:MaxFileSize", 20 * 1024 * 1024);

    private int Int(string key, int fallback) =>
        int.TryParse(configuration[key], out var value) ? value : fallback;
}
