namespace TreasuryPortal.Web.Http;

public static class ApiProtection
{
    /// <summary>Его ставит интерсептор apiHeadersInterceptor в Angular на каждый запрос к api/.</summary>
    public const string RequiredHeader = "X-Requested-With";

    private const string ApiPrefix = "/api";
    private const string HubPrefix = "/hubs";

    /// <summary>
    /// CSRF-защита без cookie. Windows-аутентификация, как и cookie, «амбиентная»: браузер сам
    /// проходит handshake и для запроса, который отправила чужая страница. Поэтому изменяющие
    /// запросы к API принимаются, только если браузер пометил их как same-origin (Fetch Metadata)
    /// и в них есть заголовок, который кросс-сайтовая форма поставить не может, а fetch с чужого
    /// origin — только через CORS-preflight, которого сервер не разрешает.
    ///
    /// У хабов SignalR правило строже: same-origin требуется от любого запроса, включая GET.
    /// Открытие WebSocket — это GET, заголовок X-Requested-With браузер в него поставить
    /// не может, и без этой проверки чужая страница открыла бы сокет к хабу уже
    /// аутентифицированным пользователем (cross-site WebSocket hijacking).
    /// </summary>
    public static IApplicationBuilder UseApiProtection(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var request = context.Request;
            var isApi = request.Path.StartsWithSegments(ApiPrefix);
            var isHub = !isApi && request.Path.StartsWithSegments(HubPrefix);

            if (!isApi && !isHub)
            {
                return next(context);
            }

            if (isApi)
            {
                // Ответы API не должны оседать в кэше браузера и прокси — прогрев в том числе
                context.Response.Headers.CacheControl = "no-store";
            }

            if (IsAllowed(request, isHub))
            {
                return next(context);
            }

            // Тело (ProblemDetails) допишет UseStatusCodePages
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        });

    private static bool IsAllowed(HttpRequest request, bool isHub)
    {
        if (!IsSameOrigin(request))
        {
            return false;
        }

        // Хабу довольно same-origin: заголовок в открытие WebSocket не положить,
        // а negotiate-запрос его несёт — его ставит клиент SignalR
        return isHub || IsSafe(request.Method) || request.Headers.ContainsKey(RequiredHeader);
    }

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
        HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);

    /// <summary>
    /// Браузеры без Fetch Metadata и не-браузерные клиенты заголовок не шлют — для них
    /// решает X-Requested-With, а для хаба проверка в таком случае не срабатывает вовсе.
    /// </summary>
    private static bool IsSameOrigin(HttpRequest request)
    {
        var site = request.Headers["Sec-Fetch-Site"].ToString();
        return site.Length == 0 || string.Equals(site, "same-origin", StringComparison.OrdinalIgnoreCase);
    }
}
