using TreasuryPortal.Web.Configuration.Abstract;

namespace TreasuryPortal.Web.Configuration;

/// <inheritdoc cref="ISettingsHolder"/>
public sealed class SettingsHolder(IConfiguration configuration) : ISettingsHolder
{
    public ISettingsStore Store { get; } = new SettingsStore(configuration);
}
