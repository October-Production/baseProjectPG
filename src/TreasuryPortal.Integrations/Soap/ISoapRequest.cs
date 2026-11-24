namespace TreasuryPortal.Integrations.Soap;

/// <summary>
/// Запрос SOAP-операции, который знает тип своего ответа. Один класс описывает операцию целиком:
/// элемент и пространство имён — [XmlRoot], SOAPAction — [SoapAction], поля — свойства
/// с [XmlElement]. Ответ — такой же класс со своим [XmlRoot]:
///
///   [XmlRoot("getDeals4Treasury", Namespace = B2Xml.Namespace)]
///   [SoapAction("getDeals4Treasury")]
///   public sealed class GetDeals4TreasuryRequest : ISoapRequest&lt;GetDeals4TreasuryResponse&gt;
///   {
///       [XmlElement("accountNo")] public string AccountNo { get; set; } = "";
///   }
///
///   var response = await soap.CallAsync(user, new GetDeals4TreasuryRequest { AccountNo = … }, ct);
///
/// Классы можно написать руками или получить из WSDL (dotnet-svcutil --serializer XmlSerializer)
/// и из XSD (xsd.exe): подходят любые публичные классы, понятные XmlSerializer.
/// </summary>
/// <typeparam name="TResponse">Класс ответа: элемент операции из soap:Body.</typeparam>
public interface ISoapRequest<TResponse> where TResponse : class
{
}
