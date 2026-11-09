using TreasuryPortal.DbCore.Repositories.Abstract;

namespace TreasuryPortal.DbCore.UOW.Abstract;

/// <summary>
/// Единственная точка входа в данные портала. Приложение берёт это из DI и никогда не создаёт
/// репозиторий само — иначе каждая сущность начала бы собирать строку подключения по-своему.
///
/// Добавляя сущность, допишите сюда одно свойство и одну строку в <see cref="TreasuryUow"/>.
/// </summary>
public interface ITreasuryUow
{
    /// <summary>Веб-роли и права пользователя.</summary>
    IAuthorizationRepository Authorization { get; }

    /// <summary>Справочники: валюты, типы операций, статусы.</summary>
    IDictionaryRepository Dictionaries { get; }

    // Дальше — сущности портала, по одному свойству на каждую:
    // IPaymentOrderRepository PaymentOrders { get; }
    // ILimitRepository Limits { get; }
}
