namespace TreasuryPortal.Integrations.Soap;

/// <summary>
/// SOAPAction операции — как в WSDL (атрибут soapAction у операции). Если шина ждёт другое
/// значение, его задают в настройках сервиса, не меняя код:
/// Configuration:ServiceLinks:&lt;Сервис&gt;:Actions:&lt;операция&gt;.
/// Нет ни атрибута, ни настройки — уходит пустой SOAPAction.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SoapActionAttribute(string action) : Attribute
{
    public string Action { get; } = action;
}
