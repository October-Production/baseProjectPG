using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SpaServices;
using Microsoft.AspNetCore.StaticFiles;

namespace TreasuryPortal.Web.Spa;

/// <summary>
/// Хостинг клиента. Класс нужен и на сервере, и при отладке, но ветки у него разные:
/// в Production отдаётся собранный клиент из wwwroot, а при отладке всё, для чего у бэкенда
/// нет своего адреса, проксируется на ng serve. Дев-ветка включается ключом Spa:DevServer:Url,
/// который задан только в appsettings.Development.json.
/// </summary>
public static partial class SpaHosting
{
    /// <summary>
    /// Регистрация. Запуск ng serve добавляется ТОЛЬКО ПРИ ОТЛАДКЕ — когда задан Spa:DevServer:Url.
    /// </summary>
    public static WebApplicationBuilder AddSpaHosting(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(SpaDevServerOptions.SectionName);
        builder.Services.Configure<SpaDevServerOptions>(section);

        if (!string.IsNullOrEmpty(section["Url"]))
        {
            builder.Services.AddHostedService<SpaDevServerLauncher>();
        }

        return builder;
    }

    /// <summary>
    /// Маршруты клиента: при отладке — прокси на ng serve, на сервере — статика из wwwroot
    /// и index.html с подменой base href.
    /// </summary>
    public static WebApplication MapSpa(this WebApplication app)
    {
        var devServer = app.Configuration.GetSection(SpaDevServerOptions.SectionName).Get<SpaDevServerOptions>();
        if (!string.IsNullOrEmpty(devServer?.Url))
        {
            app.UseSpaDevServerProxy(devServer);
            return app;
        }

        // IIS: собранный клиент из wwwroot. UseStaticFiles стоит после UseAuthorization, поэтому
        // fallback-политика закрывает и статику — даже если в IIS по ошибке включат анонимный доступ.
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = SetCacheHeaders });
        app.MapFallback(ServeIndexHtml);
        return app;
    }

    /// <summary>
    /// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development).
    ///
    /// Браузер ходит только сюда — Kestrel или IIS Express проводят Windows-аутентификацию сами,
    /// а всё, для чего у бэкенда нет своего адреса, уходит на ng serve. Обратная схема (ng serve
    /// с proxy.conf на API) ломает NTLM: прокси не держит соединения 1:1, шаги handshake
    /// разъезжаются по разным сокетам бэкенда — отсюда 400.
    ///
    /// UseSpa — терминальное middleware, а в minimal hosting исполнение эндпоинтов добавляется
    /// в конец конвейера. Поэтому ветку надо заводить через MapWhen и пускать в неё только
    /// запросы к клиенту, иначе прокси перехватит адреса бэкенда раньше, чем до них дойдёт очередь.
    /// </summary>
    private static void UseSpaDevServerProxy(this WebApplication app, SpaDevServerOptions devServer)
    {
        app.MapWhen(IsClientRequest, spaBranch =>
        {
            spaBranch.Use(ChallengeAnonymous);

            spaBranch.UseSpa(spa =>
            {
                spa.Options.SourcePath = devServer.SourcePath;
                spa.UseProxyToSpaDevelopmentServer(devServer.Url!);
            });
        });
    }

    /// <summary>
    /// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development): запрос к клиенту — тот, для которого у бэкенда нет
    /// эндпоинта. Маршрутизация к этому моменту уже отработала, поэтому /health, описание API
    /// и контроллеры сюда не попадают сами, без перечисления адресов.
    ///
    /// /api и /hubs исключены целиком: неизвестный адрес под ними — это 404 бэкенда,
    /// а не страница клиента.
    /// </summary>
    private static bool IsClientRequest(HttpContext context) =>
        context.GetEndpoint() is null &&
        !context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/hubs");

    /// <summary>
    /// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development): держит отладку в тех же условиях, что и сервер.
    ///
    /// MapWhen уводит запрос в ветку middleware, где эндпоинтов нет, а значит не работает
    /// и fallback-политика. Без этой проверки index.html и статика в отладке отдавались бы
    /// анонимно, и первый handshake случался бы уже из JS — не так, как на IIS с выключенным
    /// Anonymous.
    ///
    /// Исключение — открытие WebSocket: по спецификации рукопожатие считается неудачным
    /// на любом ответе, кроме 101, поэтому ни один браузер не отвечает на 401 с
    /// WWW-Authenticate: Negotiate — у WebSocket API просто нет для этого механизма.
    /// HMR-сокет ng serve из-за этого не поднялся бы вовсе. За ним только локальный ng serve,
    /// данных приложения там нет.
    /// </summary>
    private static async Task ChallengeAnonymous(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated == true || IsWebSocketUpgrade(context.Request))
        {
            await next(context);
            return;
        }

        await context.ChallengeAsync();
    }

    private static bool IsWebSocketUpgrade(HttpRequest request) =>
        string.Equals(request.Headers.Upgrade.ToString(), "websocket", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// index.html с base href = PathBase: один и тот же билд работает и в корне сайта, и в
    /// виртуальном приложении IIS (https://host/TreasuryPortal/). Без кэша — после деплоя
    /// пользователь сразу получает новую версию.
    /// </summary>
    private static async Task<IResult> ServeIndexHtml(HttpContext context, IWebHostEnvironment environment)
    {
        var file = environment.WebRootFileProvider.GetFileInfo("index.html");
        if (!file.Exists || file.PhysicalPath is null)
        {
            return TypedResults.Problem(
                detail: "wwwroot/index.html не найден: клиент не собран. Публикуйте через dotnet publish.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var html = await File.ReadAllTextAsync(file.PhysicalPath, context.RequestAborted);
        var baseHref = HtmlEncoder.Default.Encode($"{context.Request.PathBase}/");
        html = BaseHrefTag().Replace(html, _ => $"<base href=\"{baseHref}\">", 1);

        context.Response.Headers.CacheControl = "no-cache";
        return TypedResults.Content(html, "text/html", Encoding.UTF8);
    }

    /// <summary>Файлы с хэшем в имени (main-K5PEHX5T.js, chunk-…, styles-…) неизменяемы — кэш на год.</summary>
    private static void SetCacheHeaders(StaticFileResponseContext context)
    {
        if (HashedAssetName().IsMatch(context.File.Name))
        {
            context.Context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        }
    }

    [GeneratedRegex("""<base\s+href="[^"]*"\s*/?>""", RegexOptions.IgnoreCase)]
    private static partial Regex BaseHrefTag();

    [GeneratedRegex(@"-[A-Z0-9]{8}\.(?:js|css)$")]
    private static partial Regex HashedAssetName();
}
