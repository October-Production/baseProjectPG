using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.Integrations.Rest;

/// <summary>
/// Вызов REST-сервиса с JSON — то же, что RestRunner в kaznacms, но через фабрику HttpClient:
/// без нового HttpClient на каждый вызов, с тайм-аутом, журналом и понятной ошибкой.
///
/// Путь запроса отсчитывается от Endpoint: Endpoint https://mi.bank/api и путь deals/42 дают
/// https://mi.bank/api/deals/42, пустой путь — сам Endpoint. Значение из пользовательского ввода
/// подставляйте в путь через Uri.EscapeDataString.
///
/// JSON читается без учёта регистра имён и пишется в camelCase. Ответ 404 становится 404
/// портала («запис не знайдено»), остальные ошибки сервиса — 502.
///
/// Применение в сервисе:
///   var deal = await _rest.GetAsync&lt;Deal&gt;(user, $"deals/{Uri.EscapeDataString(id)}", cancellationToken);
/// </summary>
public sealed class RestClient
{
    private readonly ExternalCaller _caller;

    internal RestClient(ExternalCaller caller) => _caller = caller;

    public Task<T?> GetAsync<T>(IUserCredential user, string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(user, HttpMethod.Get, path, body: null, cancellationToken);

    public Task<T?> PostAsync<T>(
        IUserCredential user, string path, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(user, HttpMethod.Post, path, body, cancellationToken);

    /// <summary>Любой метод. Ответ без тела (например, 204) — default.</summary>
    public Task<T?> SendAsync<T>(
        IUserCredential user,
        HttpMethod method,
        string path,
        object? body = null,
        CancellationToken cancellationToken = default) =>
        _caller.CallAsync(
            user,
            operation: $"{method.Method} {path.Split('?', 2)[0]}",
            journalRequest: new { path, body },
            (options, endpoint) => CreateRequest(options, endpoint, method, path, body),
            ReadAsync<T>,
            cancellationToken);

    private HttpRequestMessage CreateRequest(
        ExternalServiceOptions options, Uri endpoint, HttpMethod method, string path, object? body)
    {
        if (_caller.Auth(options) == ExternalAuth.WsSecurity)
        {
            throw new ExternalServiceException(
                _caller.Service,
                $"Неправильне налаштування сервісу {_caller.Title(options)}",
                503,
                new InvalidOperationException("WS-Security бывает только у SOAP; для REST укажите Basic, Bearer или Windows"));
        }

        var request = new HttpRequestMessage(method, Combine(endpoint, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Helper.SerializerOptions);
        }

        return request;
    }

    private async Task<T?> ReadAsync<T>(
        HttpResponseMessage response, ExternalServiceOptions options, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ExternalServiceException(
                _caller.Service,
                $"Сервіс {_caller.Title(options)}: запис не знайдено",
                404,
                new ExternalResponseException(404, ExternalCaller.Excerpt(text)));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw _caller.HttpError(options, response, text);
        }

        return string.IsNullOrWhiteSpace(text)
            ? default
            : JsonSerializer.Deserialize<T>(text, Helper.SerializerOptions);
    }

    /// <summary>
    /// Endpoint как каталог: https://host/api и путь deals дают https://host/api/deals.
    /// Абсолютный адрес в пути запрещён — иначе путь мог бы увести запрос на чужой сервер.
    /// </summary>
    private static Uri Combine(Uri endpoint, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return endpoint;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && !absolute.IsFile)
        {
            throw new ArgumentException($"Путь запроса должен быть относительным, а не {path}", nameof(path));
        }

        var root = endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/");
        return new Uri(root, path.TrimStart('/'));
    }
}
