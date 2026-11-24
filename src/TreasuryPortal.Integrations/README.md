# Внешние сервисы: как добавить сервис и метод

Пошаговый пример к разделу 5 [ARCHITECTURE.md](../../ARCHITECTURE.md). Сервис платежей
и операция `getDealDetails` у B2 здесь **вымышленные**: в реальном сервисе имена операций,
полей и пространство имён берутся из его WSDL. Код примера проверен на поддельном сервисе,
но в проект не входит: копируйте его, когда подключаете свой.

## Как идёт вызов

1. Контроллер получает запрос, проверяет право (`[AuthorizePerm]`) и вызывает метод сервиса.
2. Сервис заполняет класс запроса и вызывает `soap.CallAsync(user, request, ct)`.
3. `SoapClient` берёт настройки своего раздела на момент вызова: адрес, вход, тайм-аут, SOAPAction.
   Дальше он сериализует запрос, собирает конверт и пишет начало операции в системный журнал.
4. Ответ проверяется на Fault и разбирается в класс ответа.
5. Сервис переводит ответ в модель портала, контроллер отдаёт её как JSON.
6. Если что-то пошло не так, выбрасывается `ExternalServiceException`. `ApiExceptionHandler`
   превращает его в 502 или 504 с понятным текстом, подробности уходят в лог и в журнал.

## Новый сервис

Пример — SOAP-сервис платежей с двумя операциями: список `getPayments` и карточка `getPayment`.
Раздел настроек — `PaymentsService`.

### Шаг 1. Контракты: один файл на операцию

`Services/Payments/Contracts/`. Классы публичные, со свойствами `get; set;` — так требует
`XmlSerializer`. Пространство имён задаётся явно: у корня в `[XmlRoot]`, у вложенных классов
в `[XmlType]`.

```csharp
// Services/Payments/Contracts/PaymentsXml.cs
namespace TreasuryPortal.Integrations.Services.Payments.Contracts;

/// <summary>Пространство имён операций сервиса — одно на все его контракты.</summary>
public static class PaymentsXml
{
    public const string Namespace = "http://bank/payments/";
}
```

```csharp
// Services/Payments/Contracts/GetPayments.cs — список
using System.Xml.Serialization;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Services.Payments.Contracts;

[XmlRoot("getPayments", Namespace = PaymentsXml.Namespace)]
[SoapAction("getPayments")]
public sealed class GetPaymentsRequest : ISoapRequest<GetPaymentsResponse>
{
    [XmlElement("accountNo")]
    public string AccountNo { get; set; } = "";
}

[XmlRoot("getPaymentsResponse", Namespace = PaymentsXml.Namespace)]
public sealed class GetPaymentsResponse
{
    [XmlElement("payment")]
    public List<PaymentItem> Items { get; set; } = [];
}

[XmlType(Namespace = PaymentsXml.Namespace)]
public sealed class PaymentItem
{
    [XmlElement("id")]
    public string Id { get; set; } = "";

    [XmlElement("amount")]
    public decimal Amount { get; set; }

    [XmlElement("status")]
    public string Status { get; set; } = "";
}
```

```csharp
// Services/Payments/Contracts/GetPayment.cs — карточка
using System.Xml.Serialization;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Services.Payments.Contracts;

[XmlRoot("getPayment", Namespace = PaymentsXml.Namespace)]
[SoapAction("getPayment")]
public sealed class GetPaymentRequest : ISoapRequest<GetPaymentResponse>
{
    [XmlElement("id")]
    public string Id { get; set; } = "";
}

[XmlRoot("getPaymentResponse", Namespace = PaymentsXml.Namespace)]
public sealed class GetPaymentResponse
{
    /// <summary>Нет элемента — платежа с таким id нет.</summary>
    [XmlElement("payment")]
    public PaymentCard? Payment { get; set; }
}

[XmlType(Namespace = PaymentsXml.Namespace)]
public sealed class PaymentCard
{
    [XmlElement("id")]
    public string Id { get; set; } = "";

    [XmlElement("amount")]
    public decimal Amount { get; set; }

    [XmlElement("status")]
    public string Status { get; set; } = "";

    [XmlElement("purpose")]
    public string Purpose { get; set; } = "";
}
```

### Шаг 2. Модели портала

То, что увидит клиент. От XML шины модели не зависят: переименование поля в сервисе
до клиента не дойдёт.

```csharp
// Services/Payments/Payment.cs
namespace TreasuryPortal.Integrations.Services.Payments;

public sealed record Payment(string Id, decimal Amount, string Status);

public sealed record PaymentDetails(string Id, decimal Amount, string Status, string Purpose);
```

### Шаг 3. Интерфейс

```csharp
// Services/Payments/Abstract/IPaymentsService.cs
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.Integrations.Services.Payments.Abstract;

public interface IPaymentsService
{
    Task<IReadOnlyList<Payment>> ListAsync(
        IUserCredential user, string accountNumber, CancellationToken cancellationToken = default);

    Task<PaymentDetails> GetAsync(IUserCredential user, string id, CancellationToken cancellationToken = default);
}
```

### Шаг 4. Реализация

`SoapClient` приходит в конструктор уже привязанным к разделу, поэтому адреса и паролей здесь нет.

```csharp
// Services/Payments/PaymentsService.cs
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.Integrations.Services.Payments.Abstract;
using TreasuryPortal.Integrations.Services.Payments.Contracts;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Services.Payments;

internal sealed class PaymentsService(SoapClient soap) : IPaymentsService
{
    /// <summary>Раздел настроек Configuration:ServiceLinks:PaymentsService.</summary>
    public const string Name = "PaymentsService";

    public async Task<IReadOnlyList<Payment>> ListAsync(
        IUserCredential user, string accountNumber, CancellationToken cancellationToken = default)
    {
        var response = await soap.CallAsync(user, new GetPaymentsRequest { AccountNo = accountNumber }, cancellationToken);
        return [.. response.Items.Select(item => new Payment(item.Id, item.Amount, item.Status))];
    }

    public async Task<PaymentDetails> GetAsync(
        IUserCredential user, string id, CancellationToken cancellationToken = default)
    {
        var response = await soap.CallAsync(user, new GetPaymentRequest { Id = id }, cancellationToken);
        var card = response.Payment ?? throw new ExternalServiceException(Name, "Платіж не знайдено", 404);
        return new PaymentDetails(card.Id, card.Amount, card.Status, card.Purpose);
    }
}
```

### Шаг 5. Регистрация

Одна строка в `AddIntegrations` (`IntegrationsServiceCollectionExtensions.cs`). Она регистрирует
сразу настройки раздела, HttpClient со своим пулом соединений и сам сервис.

```csharp
services.AddSoapService<IPaymentsService, PaymentsService>(configuration, PaymentsService.Name);
```

### Шаг 6. Настройки

- **Секреты — в Vault:** `Configuration:ServiceLinks:PaymentsService:Endpoint`, `User`, `PWD`.
- **Значения по умолчанию без секретов — в `appsettings.json`**, в разделе `Configuration:ServiceLinks`:

  ```json
  "PaymentsService": { "Title": "Платежі", "TimeoutSeconds": 15 }
  ```

- **При необходимости:** `Auth`, если вход не WS-Security; `SoapVersion: Soap12`;
  `Actions:getPayments`, если шина ждёт другой SOAPAction.

### Шаг 7. Право

- Константа `PaymentRead = "TPPaymentRead"` в `AppPermissions` (`Web/Auth/AppAccess.cs`) и в списке `All`.
- Та же константа в клиенте (`core/auth/access.model.ts`).
- Право выдаётся в базе.

### Шаг 8. Контроллер

По образцу `Web/Controllers/B2Controller.cs`. Право на контроллере закрывает все его действия.

```csharp
// Web/Controllers/PaymentsController.cs
[Route("api/payments")]
[AuthorizePerm(AppPermissions.PaymentRead)]
public sealed class PaymentsController(
    IPaymentsService payments, ITreasuryUow uow, ISettingsHolder settings, CurrentUser currentUser)
    : ControllerCore(uow, settings, currentUser)
{
    /// <summary>GET api/payments?accountNumber=… — список.</summary>
    [HttpGet]
    public Task<IReadOnlyList<Payment>> ListAsync([FromQuery] string accountNumber, CancellationToken cancellationToken) =>
        payments.ListAsync(Me, accountNumber.Trim(), cancellationToken);

    /// <summary>GET api/payments/{id} — карточка.</summary>
    [HttpGet("{id}")]
    public Task<PaymentDetails> GetAsync(string id, CancellationToken cancellationToken) =>
        payments.GetAsync(Me, id, cancellationToken);
}
```

`SoapClient`, обработку ошибок, журнал и `Program.cs` при этом трогать не нужно.

## Новый метод в существующем сервисе

Пример — карточка угоды в B2 через операцию `getDealDetails`. Регистрация, раздел настроек,
адрес и учётка у B2 уже есть, поэтому шагов меньше.

### Шаг 1. Контракт

```csharp
// Services/B2/Contracts/GetDealDetails.cs
using System.Xml.Serialization;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Services.B2.Contracts;

[XmlRoot("getDealDetails", Namespace = B2Xml.Namespace)]
[SoapAction("getDealDetails")]
public sealed class GetDealDetailsRequest : ISoapRequest<GetDealDetailsResponse>
{
    [XmlElement("dealId")]
    public string DealId { get; set; } = "";
}

[XmlRoot("getDealDetailsResponse", Namespace = B2Xml.Namespace)]
public sealed class GetDealDetailsResponse
{
    [XmlElement("deal")]
    public DealDetailsItem? Deal { get; set; }
}

[XmlType(Namespace = B2Xml.Namespace)]
public sealed class DealDetailsItem
{
    [XmlElement("dealId")]
    public string DealId { get; set; } = "";

    [XmlElement("dealNo")]
    public string DealNo { get; set; } = "";

    [XmlElement("amount")]
    public decimal Amount { get; set; }
}
```

### Шаг 2. Модель, если для карточки нужна своя

```csharp
// Services/B2/B2DealDetails.cs
public sealed record B2DealDetails(string DealId, string DealNumber, decimal Amount);
```

### Шаг 3. Метод в интерфейсе и в сервисе

```csharp
// Services/B2/Abstract/IB2Service.cs
Task<B2DealDetails> GetDealAsync(IUserCredential user, string dealId, CancellationToken cancellationToken = default);

// Services/B2/B2Service.cs
public async Task<B2DealDetails> GetDealAsync(
    IUserCredential user, string dealId, CancellationToken cancellationToken = default)
{
    var response = await soap.CallAsync(user, new GetDealDetailsRequest { DealId = dealId }, cancellationToken);
    var deal = response.Deal ?? throw new ExternalServiceException(Name, "Угоду не знайдено", 404);
    return new B2DealDetails(deal.DealId, deal.DealNo, deal.Amount);
}
```

### Шаг 4. Действие в контроллере

В тот же `B2Controller`. Право на контроллере уже стоит и распространяется на новое действие.

```csharp
/// <summary>GET api/b2/deals/{dealId} — картка угоди.</summary>
[HttpGet("deals/{dealId}")]
public Task<B2DealDetails> GetDealAsync(string dealId, CancellationToken cancellationToken) =>
    b2.GetDealAsync(Me, dealId, cancellationToken);
```

### Шаг 5. Только если нужно

Если шина ждёт другой SOAPAction — `Configuration:ServiceLinks:B2Service:Actions:getDealDetails`
в Vault.

Регистрацию, настройки, `Program.cs` и `SoapClient` трогать не нужно. Новая операция — это один
файл контракта, один метод в сервисе и одно действие в контроллере.

## Если сервис REST

Шаги те же, отличий три:

- контракты — обычные классы или записи для JSON, без XML-атрибутов;
- регистрация — `AddRestService` вместо `AddSoapService`;
- в сервисе вместо контракта — путь и тело запроса.

```csharp
// Services/Rates/RatesService.cs — вымышленный сервис курсов
internal sealed class RatesService(RestClient rest) : IRatesService
{
    public const string Name = "RatesService";

    public async Task<IReadOnlyList<Rate>> ListAsync(
        IUserCredential user, string currency, CancellationToken cancellationToken = default) =>
        await rest.GetAsync<List<Rate>>(user, $"rates?currency={Uri.EscapeDataString(currency)}", cancellationToken) ?? [];

    // Ответ 404 RestClient сам превращает в 404 «Сервіс …: запис не знайдено»
    public async Task<Rate> GetAsync(IUserCredential user, string id, CancellationToken cancellationToken = default) =>
        await rest.GetAsync<Rate>(user, $"rates/{Uri.EscapeDataString(id)}", cancellationToken)
            ?? throw new ExternalServiceException(Name, "Курс не знайдено", 404);
}

// AddIntegrations
services.AddRestService<IRatesService, RatesService>(configuration, RatesService.Name);
```

Путь отсчитывается от `Endpoint` раздела. Значение из пользовательского ввода в пути — только
через `Uri.EscapeDataString`.
