namespace TreasuryPortal.Web.Http;

public static class SecurityHeaders
{
    /// <summary>
    /// Windows-аутентификация срабатывает без участия пользователя, поэтому чужая страница во
    /// фрейме увидела бы приложение уже авторизованным — фреймы запрещены (clickjacking).
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Frame-Options"] = "DENY";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "same-origin";
            return next(context);
        });
}
