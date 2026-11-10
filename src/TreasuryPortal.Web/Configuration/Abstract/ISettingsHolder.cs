namespace TreasuryPortal.Web.Configuration.Abstract;

/// <summary>
/// Держатель настроек — как в BanknoteOps и NBC: регистрируется в Program.cs сразу после
/// VaultInit() и отдаёт <see cref="ISettingsStore"/> всем, кому нужны настройки.
/// </summary>
public interface ISettingsHolder
{
    ISettingsStore Store { get; }
}
