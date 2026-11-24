namespace TreasuryPortal.Integrations.Soap;

/// <summary>SOAP Fault из ответа сервиса: код, текст и подробности — как их прислал сервис.</summary>
public sealed class SoapFaultException(string code, string reason, string? detail)
    : Exception(string.IsNullOrEmpty(detail) ? $"SOAP Fault {code}: {reason}" : $"SOAP Fault {code}: {reason}; {detail}")
{
    /// <summary>faultcode (SOAP 1.1) или Code/Value (SOAP 1.2).</summary>
    public string Code { get; } = code;

    /// <summary>faultstring (SOAP 1.1) или Reason/Text (SOAP 1.2).</summary>
    public string Reason { get; } = reason;

    /// <summary>Содержимое detail (SOAP 1.1) или Detail (SOAP 1.2) как XML.</summary>
    public string? Detail { get; } = detail;
}
