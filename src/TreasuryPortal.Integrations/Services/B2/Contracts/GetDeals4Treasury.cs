using System.Xml.Serialization;
using TreasuryPortal.Integrations.Soap;

namespace TreasuryPortal.Integrations.Services.B2.Contracts;

/// <summary>Пространство имён операций B2 на интеграционной шине.</summary>
public static class B2Xml
{
    public const string Namespace = "http://sab/";
}

/// <summary>
/// Поиск угод для казначейства — операция getDeals4Treasury. Один файл на операцию:
/// запрос, ответ и их части рядом.
///
/// SOAPAction — как в WSDL. В kaznacms он брался из Vault (DealSearchMethod); если шина ждёт
/// другое значение, задайте его в Configuration:ServiceLinks:B2Service:Actions:getDeals4Treasury.
/// </summary>
[XmlRoot("getDeals4Treasury", Namespace = B2Xml.Namespace)]
[SoapAction("getDeals4Treasury")]
public sealed class GetDeals4TreasuryRequest : ISoapRequest<GetDeals4TreasuryResponse>
{
    [XmlElement("accountNo")]
    public string AccountNo { get; set; } = "";

    [XmlElement("currencyId")]
    public string CurrencyId { get; set; } = "";
}

[XmlRoot("getDeals4TreasuryResponse", Namespace = B2Xml.Namespace)]
public sealed class GetDeals4TreasuryResponse
{
    [XmlElement("Deals4TreasuryItem")]
    public List<Deals4TreasuryItem> Items { get; set; } = [];
}

/// <summary>
/// Пространство имён у вложенных классов задано явно: так контракт не зависит от того,
/// как XmlSerializer наследует его от корня.
/// </summary>
[XmlType(Namespace = B2Xml.Namespace)]
public sealed class Deals4TreasuryItem
{
    [XmlElement("dealId")]
    public string DealId { get; set; } = "";

    [XmlElement("dealNo")]
    public string DealNo { get; set; } = "";

    /// <summary>Строкой: формат даты у B2 не xsd:dateTime, разбирать его здесь не беремся.</summary>
    [XmlElement("dealDate")]
    public string DealDate { get; set; } = "";

    [XmlElement("dealTypeSName")]
    public string DealTypeSName { get; set; } = "";
}
