using Microsoft.AspNetCore.Diagnostics;
using TreasuryPortal.DbCore.Utils;
using TreasuryPortal.Integrations;

namespace TreasuryPortal.Web.Http;

/// <summary>
/// Единственное место, где необработанное исключение превращается в ответ. Стек остаётся в логе
/// сервера, наружу уходит только сообщение. <see cref="MessageException"/> и
/// <see cref="ExternalServiceException"/> (сбой внешнего сервиса) — это уже готовый для
/// пользователя текст вместе с кодом ответа.
///
/// Работает и для контроллеров, и для оставшихся минимальных эндпоинтов, поэтому отдельный
/// фильтр MVC (как ExceptionFilter в NBC) не нужен.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Клиент ушёл — отвечать некому и нечем
        if (httpContext.RequestAborted.IsCancellationRequested || httpContext.Response.HasStarted)
        {
            return true;
        }

        // Кривое тело запроса или слишком большой запрос — это 400, а не 500: ASP.NET несёт нужный
        // код в самом исключении. Сбой внешнего сервиса тоже несёт свой код (502, 504) и текст
        var (message, statusCode) = exception switch
        {
            BadHttpRequestException bad => (bad.Message, bad.StatusCode),
            ExternalServiceException external => (external.Message, external.StatusCode),
            _ => FromHelper(exception),
        };

        // Подробности сбоя внешнего сервиса уже записал его клиент: здесь второй раз стек не нужен
        if (statusCode >= 500 && exception is not ExternalServiceException)
        {
            logger.LogError(exception, "Необработанная ошибка на {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Запрос {Path} отклонён: {Message}", httpContext.Request.Path, message);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new { title = message, status = statusCode },
            cancellationToken);

        return true;
    }

    private static (string Message, int StatusCode) FromHelper(Exception exception)
    {
        var (message, _, statusCode) = Helper.GetExceptionText(exception);
        return (message, statusCode);
    }
}
