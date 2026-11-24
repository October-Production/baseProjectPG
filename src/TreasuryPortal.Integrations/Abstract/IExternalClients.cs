using TreasuryPortal.Integrations.Rest;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Abstract;

/// <summary>
/// Клиенты внешних сервисов по имени — имя раздела Configuration:ServiceLinks.
///
/// Обычно сервис получает свой клиент в конструктор: AddSoapService / AddRestService берут его
/// отсюда и привязывают к разделу. Напрямую фабрика нужна классу, который ходит в несколько
/// сервисов сразу (их разделы регистрируются через AddExternalService):
///
///   private readonly SoapClient _b2 = clients.Soap("B2Service");
///
/// Клиенты лёгкие и без состояния: держать их в поле сервиса-одиночки можно и нужно.
/// </summary>
public interface IExternalClients
{
    SoapClient Soap(string service);

    RestClient Rest(string service);
}
