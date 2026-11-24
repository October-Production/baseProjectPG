using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.Integrations.Services.B2.Abstract;

/// <summary>
/// Банковская система B2 через интеграционную шину (SOAP, WS-Security). Перенесено из kaznacms
/// (B2Service) и служит образцом внешнего сервиса: копируйте его, когда подключаете свой.
/// Новая операция — класс контракта в Contracts/ и метод здесь.
/// </summary>
public interface IB2Service
{
    /// <summary>
    /// Угоди по рахунку і валюті (операция getDeals4Treasury). Ничего не нашлось — пустой список;
    /// сбой B2 — <see cref="ExternalServiceException"/>.
    /// </summary>
    Task<IReadOnlyList<B2Deal>> SearchDealsAsync(
        IUserCredential user, string accountNumber, string currencyCode, CancellationToken cancellationToken = default);
}
