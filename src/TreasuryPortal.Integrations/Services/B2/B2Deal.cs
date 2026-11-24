namespace TreasuryPortal.Integrations.Services.B2;

/// <summary>
/// Угода з B2 в том виде, в каком её получает портал. Контракт операции
/// (<see cref="Contracts.Deals4TreasuryItem"/>) повторяет XML шины; модель от него не зависит,
/// поэтому переименование поля в B2 не дойдёт до клиента.
/// </summary>
/// <param name="DealId">Внутренний идентификатор угоды в B2.</param>
/// <param name="DealNumber">Номер угоды.</param>
/// <param name="DealDate">Дата угоды строкой, как её прислал B2.</param>
/// <param name="DealTypeName">Краткое название типа угоды.</param>
public sealed record B2Deal(string DealId, string DealNumber, string DealDate, string DealTypeName);
