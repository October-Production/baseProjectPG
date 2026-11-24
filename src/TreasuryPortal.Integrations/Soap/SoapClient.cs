using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.Integrations.Soap;

/// <summary>
/// Вызов SOAP-сервиса. Основной способ — по контракту (<see cref="ISoapRequest{TResponse}"/>):
/// операция описана классами запроса и ответа, конверт и разбор одинаковы для всех операций
/// всех сервисов. Для редкой операции, которую неудобно описать классами, остался вызов
/// с XElement — как SOAPRunner в kaznacms.
///
/// Что сделано иначе, чем в kaznacms:
/// - HttpClient берётся из фабрики по имени сервиса, а не из статического поля, и сертификат
///   сервера проверяется (в kaznacms проверка была выключена);
/// - адрес, логин и пароль читаются из настроек на каждом вызове — ротация в Vault действует
///   без перезапуска;
/// - вызов пишется в системный журнал, а сбой превращается в <see cref="ExternalServiceException"/>
///   с текстом для пользователя и кодом 502 или 504;
/// - поддержаны SOAP 1.1 и 1.2, вход через WS-Security (UsernameToken) или Basic.
///
/// Применение в сервисе (SoapClient приходит в конструктор, см. AddSoapService):
///   var response = await soap.CallAsync(user, new GetDeals4TreasuryRequest { … }, cancellationToken);
/// </summary>
public sealed class SoapClient
{
    private static readonly XNamespace Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Soap12 = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace Wsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    private const string PasswordText =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

    /// <summary>Сколько символов текста ошибки сервиса показать пользователю.</summary>
    private const int ShownReasonLength = 300;

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ExternalCaller _caller;

    internal SoapClient(ExternalCaller caller) => _caller = caller;

    /// <summary>
    /// Вызов операции по контракту: запрос собирается из класса, ответ разбирается в класс ответа.
    /// Ответ не того вида — сбой попадёт в журнал и превратится в понятную ошибку 502.
    /// </summary>
    /// <param name="user">Кто вызывает — для системного журнала.</param>
    /// <param name="request">Запрос операции; в журнал он попадает как JSON.</param>
    public Task<TResponse> CallAsync<TResponse>(
        IUserCredential user, ISoapRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : class
    {
        var operation = SoapSerializer.Operation(request.GetType());
        return SendAsync(
            user,
            operation.Name,
            journalRequest: request,
            operation.Action,
            body: () => SoapSerializer.Serialize(request),
            read: SoapSerializer.Deserialize<TResponse>,
            cancellationToken);
    }

    /// <summary>Вызов без контракта: элемент запроса собран вручную, ответ — элемент операции из soap:Body.</summary>
    /// <param name="action">SOAPAction операции, как в WSDL; бывает пустым.</param>
    /// <param name="request">Элемент операции — то, что ляжет в soap:Body.</param>
    public Task<XElement> CallAsync(
        IUserCredential user, string action, XElement request, CancellationToken cancellationToken = default) =>
        CallAsync(user, action, request, response => response, cancellationToken);

    /// <summary>То же, но ответ сразу разбирается функцией — внутри вызова, с журналом и ошибкой 502.</summary>
    public Task<T> CallAsync<T>(
        IUserCredential user,
        string action,
        XElement request,
        Func<XElement, T> read,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            user,
            request.Name.LocalName,
            journalRequest: request.ToString(SaveOptions.DisableFormatting),
            action,
            body: () => request,
            read,
            cancellationToken);

    private Task<T> SendAsync<T>(
        IUserCredential user,
        string operation,
        object? journalRequest,
        string defaultAction,
        Func<XElement> body,
        Func<XElement, T> read,
        CancellationToken cancellationToken) =>
        _caller.CallAsync(
            user,
            operation,
            journalRequest,
            (options, endpoint) => CreateRequest(options, endpoint, Action(options, operation, defaultAction), body()),
            async (response, options, token) => read(await ReadAsync(response, options, token)),
            cancellationToken);

    /// <summary>SOAPAction: настройка Actions:&lt;операция&gt; сильнее контракта.</summary>
    private static string Action(ExternalServiceOptions options, string operation, string defaultAction) =>
        options.Actions.TryGetValue(operation, out var configured) && !string.IsNullOrWhiteSpace(configured)
            ? configured.Trim()
            : defaultAction;

    private HttpRequestMessage CreateRequest(
        ExternalServiceOptions options, Uri endpoint, string action, XElement request)
    {
        var soap = options.SoapVersion == SoapVersion.Soap12 ? Soap12 : Soap11;

        var envelope = new XElement(
            soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", soap.NamespaceName),
            _caller.Auth(options) == ExternalAuth.WsSecurity
                ? new XElement(soap + "Header", Security(options))
                : null,
            new XElement(soap + "Body", request));

        var content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Utf8);
        var message = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

        if (options.SoapVersion == SoapVersion.Soap12)
        {
            // В SOAP 1.2 операция — параметр типа содержимого, отдельного заголовка нет
            content.Headers.ContentType = new MediaTypeHeaderValue("application/soap+xml") { CharSet = "utf-8" };
            if (!string.IsNullOrEmpty(action))
            {
                content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("action", Quote(action)));
            }
        }
        else
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
            message.Headers.TryAddWithoutValidation("SOAPAction", Quote(action));
        }

        return message;
    }

    private async Task<XElement> ReadAsync(
        HttpResponseMessage response, ExternalServiceOptions options, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw response.IsSuccessStatusCode
                ? new ExternalServiceException(
                    _caller.Service, $"Сервіс {_caller.Title(options)} повернув порожню відповідь", 502)
                : _caller.HttpError(options, response, text);
        }

        XElement envelope;
        try
        {
            envelope = Parse(text);
        }
        catch (XmlException)
        {
            // Обычно это HTML-страница ошибки от прокси или балансировщика, а не SOAP
            throw Unreadable(options, response, text);
        }

        var soap = envelope.Name.Namespace;
        var body = envelope.Name.LocalName == "Envelope" && (soap == Soap11 || soap == Soap12)
            ? envelope.Element(soap + "Body")
            : null;

        if (body is null)
        {
            throw Unreadable(options, response, text);
        }

        var content = body.Elements().FirstOrDefault();
        if (content is not null && content.Name == soap + "Fault")
        {
            var fault = ReadFault(content);
            var reason = fault.Reason.Length > 0 ? fault.Reason : fault.Code;
            throw new ExternalServiceException(
                _caller.Service,
                $"Сервіс {_caller.Title(options)} повернув помилку: {Shorten(reason)}",
                502,
                fault);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw _caller.HttpError(options, response, text);
        }

        // Операция без результата отвечает пустым Body
        return content ?? body;
    }

    private ExternalServiceException Unreadable(
        ExternalServiceOptions options, HttpResponseMessage response, string text) =>
        response.IsSuccessStatusCode
            ? new ExternalServiceException(
                _caller.Service,
                $"Сервіс {_caller.Title(options)} повернув відповідь, яку не вдалося розібрати",
                502,
                new ExternalResponseException((int)response.StatusCode, ExternalCaller.Excerpt(text)))
            : _caller.HttpError(options, response, text);

    private static SoapFaultException ReadFault(XElement fault)
    {
        var soap = fault.Name.Namespace;

        if (soap == Soap12)
        {
            return new SoapFaultException(
                fault.Element(soap + "Code")?.Element(soap + "Value")?.Value ?? "",
                fault.Element(soap + "Reason")?.Elements(soap + "Text").FirstOrDefault()?.Value ?? "",
                Inner(fault.Element(soap + "Detail")));
        }

        // В SOAP 1.1 дочерние элементы Fault без пространства имён, но встречаются и с ним
        return new SoapFaultException(
            Child(fault, "faultcode")?.Value ?? "",
            Child(fault, "faultstring")?.Value ?? "",
            Inner(Child(fault, "detail")));
    }

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName);

    private static string? Inner(XElement? element) =>
        element is null
            ? null
            : ExternalCaller.Excerpt(string.Concat(element.Nodes().Select(node => node.ToString(SaveOptions.DisableFormatting))));

    /// <summary>DTD в SOAP запрещены стандартом — запрещаем их и мы, а с ними и внешние сущности.</summary>
    private static XElement Parse(string text)
    {
        using var reader = XmlReader.Create(
            new StringReader(text),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XElement.Load(reader);
    }

    /// <summary>UsernameToken с паролем открытым текстом — как в kaznacms; защищает его TLS.</summary>
    private static XElement Security(ExternalServiceOptions options) =>
        new(
            Wsse + "Security",
            new XAttribute(XNamespace.Xmlns + "wsse", Wsse.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "wsu", Wsu.NamespaceName),
            new XElement(
                Wsse + "UsernameToken",
                new XElement(Wsse + "Username", options.User),
                new XElement(Wsse + "Password", new XAttribute("Type", PasswordText), options.PWD),
                new XElement(
                    Wsu + "Created",
                    DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))));

    private static string Quote(string action) => $"\"{action.Replace("\"", "\\\"")}\"";

    private static string Shorten(string text)
    {
        var line = text.Trim();
        return line.Length <= ShownReasonLength ? line : line[..ShownReasonLength] + "…";
    }
}
