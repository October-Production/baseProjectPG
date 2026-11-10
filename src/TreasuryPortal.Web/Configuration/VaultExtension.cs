using VaultSharp.Extensions.Configuration;

namespace TreasuryPortal.Web.Configuration;

/// <summary>
/// Секреты из Vault поверх appsettings: строка подключения к базе и всё остальное, чего не
/// должно быть в репозитории. Значения приходят в ту же конфигурацию, поэтому код читает их
/// обычным Configuration["Configuration:ConnectionStrings:PGSQLServer"].
///
/// Учётка для входа в Vault берётся из переменных окружения машины — ни логина, ни пароля
/// в проекте нет. Имена переменных задают константы пакета:
/// VaultEnvironmentVariableNames.LdapUser и VaultEnvironmentVariableNames.LdapPassword.
///
/// Работает одинаково и на сервере, и при отладке: на машине разработчика переменные
/// окружения дают доступ к dev-разделу Vault (MountPath в appsettings.json).
/// </summary>
public static class VaultExtension
{
    public static ConfigurationManager VaultInit(this ConfigurationManager configuration)
    {
        var vault = configuration.GetSection(VaultSettings.SectionName);

        configuration.AddVaultConfiguration(
            () => new VaultOptions(
                vault["Address"]!,
                ldapUser: Environment.GetEnvironmentVariable(VaultEnvironmentVariableNames.LdapUser),
                ldapPassword: Environment.GetEnvironmentVariable(VaultEnvironmentVariableNames.LdapPassword),
                reloadOnChange: true,
                reloadCheckIntervalSeconds: 10,
                additionalCharactersForConfigurationPath: new[] { '.' }),
            vault["MountPath"]!,
            vault["MountPoint"],
            vault["MountPathGeneral"]);

        // Следит за изменениями в Vault и подхватывает их без перезапуска приложения
        var watcher = new VaultChangeWatcher(configuration);
        _ = Task.Run(async () => await watcher.WatchAsync(CancellationToken.None));

        return configuration;
    }

    private static class VaultSettings
    {
        public const string SectionName = "VaultSettings";
    }
}
