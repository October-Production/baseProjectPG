namespace TreasuryPortal.DbCore.Repositories;

/// <summary>
/// Схема, в которой живут функции портала. Та же, что в BanknoteOps, — туда же положены
/// функции журнала и выдачи прав. Если для Treasury Portal заведут отдельную схему,
/// меняется только это значение.
/// </summary>
internal static class PgSchema
{
    public const string Name = "csen";
}
