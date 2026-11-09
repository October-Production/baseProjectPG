using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Repositories;
using TreasuryPortal.DbCore.Repositories.Abstract;
using TreasuryPortal.DbCore.UOW.Abstract;

namespace TreasuryPortal.DbCore.UOW;

/// <inheritdoc cref="ITreasuryUow"/>
/// <remarks>
/// Репозитории создаются лениво: без строки подключения приложение всё равно стартует,
/// а понятная ошибка приходит на первом обращении к данным. Это нужно для локальной отладки
/// интерфейса с подменой прав (Auth:Dev).
/// </remarks>
public sealed class TreasuryUow(PgDataSource dataSource, ISystemLog log) : ITreasuryUow
{
    private readonly Lazy<IAuthorizationRepository> _authorization =
        new(() => new AuthorizationRepository(dataSource.Primary, log));

    private readonly Lazy<IDictionaryRepository> _dictionaries =
        new(() => new DictionaryRepository(dataSource.Primary, log));

    // Новая сущность добавляется одним полем и одним свойством:
    // private readonly Lazy<IPaymentOrderRepository> _paymentOrders =
    //     new(() => new PaymentOrderRepository(dataSource.Primary, log));

    public IAuthorizationRepository Authorization => _authorization.Value;

    public IDictionaryRepository Dictionaries => _dictionaries.Value;
}
