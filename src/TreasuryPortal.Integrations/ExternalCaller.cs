using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.Integrations;

/// <summary>
/// Общая механика вызова для <see cref="Soap.SoapClient"/> и <see cref="Rest.RestClient"/>:
/// настройки на момент вызова, вход, тайм-аут, системный журнал и перевод любого сбоя
/// в <see cref="ExternalServiceException"/> с текстом для пользователя.
/// </summary>
internal sealed class ExternalCaller(
    string service,
    ExternalAuth authWithLogin,
    IHttpClientFactory httpClients,
    IOptionsMonitor<ExternalServiceOptions> options,
    ISystemLog log,
    ILogger logger)
{
    /// <summary>Сколько символов тела ответа сохранить для лога, если сервис ответил ошибкой.</summary>
    private const int ExcerptLength = 1000;

    public string Service => service;

    /// <summary>
    /// Один вызов. Запрос собирается по настройкам на этот момент, ответ разбирается внутри
    /// того же тайм-аута. Всё, что пошло не так, попадает в журнал и в лог и уходит наружу
    /// как <see cref="ExternalServiceException"/>.
    /// </summary>
    public async Task<T> CallAsync<T>(
        IUserCredential user,
        string operation,
        object? journalRequest,
        Func<ExternalServiceOptions, Uri, HttpRequestMessage> createRequest,
        Func<HttpResponseMessage, ExternalServiceOptions, CancellationToken, Task<T>> readResponse,
        CancellationToken cancellationToken)
    {
        var current = options.Get(service);
        var started = Stopwatch.GetTimestamp();
        var sent = false;

        using var logged = log.Start(user, service, operation, journalRequest);

        try
        {
            var endpoint = Endpoint(current);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Seconds(current)));

            using var request = createRequest(current, endpoint);
            Authorize(request, current);

            sent = true;
            using var response = await httpClients.CreateClient(service).SendAsync(request, timeout.Token);
            var result = await readResponse(response, current, timeout.Token);

            logger.LogDebug(
                "{Service}, {Operation}: {StatusCode} за {Elapsed} мс",
                service,
                operation,
                (int)response.StatusCode,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Пользователь ушёл со страницы: отвечать некому, и сервис тут ни при чём
            logged.Fail("Запит скасовано");
            throw;
        }
        catch (Exception ex) when (!sent && ex is not ExternalServiceException)
        {
            // Ошибка в коде, который собирает запрос, — не сбой сервиса: пусть дойдёт как есть (500)
            logged.Fail(ex);
            throw;
        }
        catch (Exception ex)
        {
            var error = ex as ExternalServiceException ?? Translate(ex, current);
            var detail = error.InnerException is OperationCanceledException ? null : error.InnerException;

            logged.Fail(detail is null ? error.Message : $"{error.Message} ({detail.Message})");
            logger.LogWarning(
                detail,
                "{Service}, {Operation}: {Message} (за {Elapsed} мс)",
                service,
                operation,
                error.Message,
                (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            if (ReferenceEquals(error, ex))
            {
                throw;
            }

            throw error;
        }
    }

    /// <summary>Как представляется портал: по настройке, а если её нет — по наличию логина.</summary>
    public ExternalAuth Auth(ExternalServiceOptions current) =>
        current.Auth ?? (string.IsNullOrEmpty(current.User) ? ExternalAuth.None : authWithLogin);

    public string Title(ExternalServiceOptions current) =>
        string.IsNullOrWhiteSpace(current.Title) ? service : current.Title.Trim();

    /// <summary>Сервис ответил HTTP-ошибкой: пользователю — код, логу — начало тела ответа.</summary>
    public ExternalServiceException HttpError(ExternalServiceOptions current, HttpResponseMessage response, string body)
    {
        var statusCode = (int)response.StatusCode;
        return new ExternalServiceException(
            service,
            $"Сервіс {Title(current)} відповів помилкою {statusCode}",
            502,
            new ExternalResponseException(statusCode, Excerpt(body)));
    }

    public static string Excerpt(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= ExcerptLength ? trimmed : trimmed[..ExcerptLength] + "…";
    }

    private Uri Endpoint(ExternalServiceOptions current)
    {
        var key = $"{ExternalServiceOptions.SectionOf(service)}:Endpoint";

        if (string.IsNullOrWhiteSpace(current.Endpoint))
        {
            throw new ExternalServiceException(
                service,
                $"Не налаштовано адресу сервісу {Title(current)}",
                503,
                new InvalidOperationException($"Пустой ключ {key}"));
        }

        if (!Uri.TryCreate(current.Endpoint.Trim(), UriKind.Absolute, out var endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttps && endpoint.Scheme != Uri.UriSchemeHttp))
        {
            throw new ExternalServiceException(
                service,
                $"Неправильна адреса сервісу {Title(current)}",
                503,
                new InvalidOperationException($"{key} = {current.Endpoint}: нужен абсолютный адрес http(s)"));
        }

        return endpoint;
    }

    private void Authorize(HttpRequestMessage request, ExternalServiceOptions current)
    {
        switch (Auth(current))
        {
            case ExternalAuth.Basic:
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{current.User}:{current.PWD}")));
                break;

            case ExternalAuth.Bearer:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.Token);
                break;

            // WsSecurity кладёт в конверт SoapClient, Windows — обработчик соединения
        }
    }

    /// <summary>Сбой без готового текста: текст выбирается по виду сбоя.</summary>
    private ExternalServiceException Translate(Exception ex, ExternalServiceOptions current) => ex switch
    {
        OperationCanceledException => new(
            service, $"Сервіс {Title(current)} не відповів за {Seconds(current)} с", 504, ex),

        HttpRequestException => new(
            service, $"Сервіс {Title(current)} недоступний", 502, ex),

        _ => new(
            service, $"Сервіс {Title(current)} повернув відповідь, яку не вдалося розібрати", 502, ex),
    };

    private static int Seconds(ExternalServiceOptions current) => Math.Max(1, current.TimeoutSeconds);
}
