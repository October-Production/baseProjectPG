// ТОЛЬКО ДЛЯ ОТЛАДКИ — заглушка, на сервере этого файла нет.
//
// Настоящий VaultInit() живёт во внутреннем пакете Util.VaultCoreLib и подмешивает секреты из
// Vault в конфигурацию. Если фид с пакетом недоступен, вместо него компилируется этот файл.
//
// Подключается из Directory.Build.targets в корне решения: он же убирает PackageReference и
// исключает Configuration/VaultExtension.cs. Внутри TreasuryPortal.Web ничего не меняется,
// поэтому обновление проекта с сервера заглушку не затирает.
//
// Чтобы вернуть настоящий Vault: удалите Directory.Build.targets и папку local-debug/,
// либо соберите с -p:UseVault=true.

namespace TreasuryPortal.Web.Configuration;

/// <summary>
/// ТОЛЬКО ДЛЯ ОТЛАДКИ (Debug либо -p:UseVault=false): на сервере вместо этого файла
/// компилируется настоящий Web/Configuration/VaultExtension.cs. Подмену делает
/// Directory.Build.targets в корне решения.
/// </summary>
public static class VaultExtension
{
    /// <summary>
    /// Ничего не делает: конфигурация остаётся такой, какой её собрали appsettings.*.json.
    /// Строку подключения на время локальной отладки кладите в appsettings.Local.json
    /// (он в .gitignore) в секцию Configuration:ConnectionStrings:PGSQLServer.
    /// </summary>
    public static ConfigurationManager VaultInit(this ConfigurationManager configuration)
    {
        return configuration;
    }
}
