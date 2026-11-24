using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.Integrations.Services.B2.Abstract;
using TreasuryPortal.Integrations.Services.B2.Contracts;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Services.B2;

/// <inheritdoc cref="IB2Service"/>
/// <remarks>
/// Сервис только заполняет контракт и переводит ответ в модель портала. Конверт, вход, тайм-аут,
/// журнал и ошибки — у <see cref="SoapClient"/>; адрес и учётка — в разделе настроек
/// <see cref="Name"/>. Ошибку B2 не нужно превращать в JSON с полем error, как в kaznacms:
/// исключение само дойдёт до клиента ответом 502 или 504 с понятным текстом.
/// </remarks>
internal sealed class B2Service(SoapClient soap) : IB2Service
{
    /// <summary>Раздел настроек Configuration:ServiceLinks:B2Service — то же имя, что в kaznacms.</summary>
    public const string Name = "B2Service";

    public async Task<IReadOnlyList<B2Deal>> SearchDealsAsync(
        IUserCredential user, string accountNumber, string currencyCode, CancellationToken cancellationToken = default)
    {
        var response = await soap.CallAsync(
            user,
            new GetDeals4TreasuryRequest { AccountNo = accountNumber, CurrencyId = currencyCode },
            cancellationToken);

        return [.. response.Items.Select(item => new B2Deal(item.DealId, item.DealNo, item.DealDate, item.DealTypeSName))];
    }
}
