using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Npgsql;

namespace TreasuryPortal.DbCore.Utils;

/// <summary>
/// Общая механика слоя данных: сериализация в JSON так, как её понимает PostgreSQL, и
/// превращение исключений в текст, который можно показать пользователю.
/// </summary>
public static class Helper
{
    /// <summary>
    /// Кириллица не экранируется в \uXXXX: такой JSON читаемо выглядит и в jsonb,
    /// и в журнале операций.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Cyrillic),
        };

    public static string ToJsonString<T>(this T item) => JsonSerializer.Serialize(item, SerializerOptions);

    public static JsonElement ToJsonElement<T>(this T item) =>
        JsonSerializer.SerializeToElement(item, SerializerOptions);

    /// <summary>
    /// Ошибка, которую функция подняла сама (RAISE EXCEPTION, SQLSTATE P0001), — это бизнес-сообщение
    /// для пользователя: берём hint, как это делает BanknoteOps. Всё остальное — неожиданный сбой:
    /// наружу идёт короткий текст, стек остаётся в details.
    /// </summary>
    public static (string Message, string? Details, int StatusCode) GetExceptionText(Exception ex)
    {
        switch (ex)
        {
            case PostgresException { SqlState: "P0001" } business:
                return (business.Hint ?? business.MessageText, null, 400);

            case PostgresException pg:
                var text = string.Concat(
                    $"{pg.SqlState}: {pg.MessageText};",
                    pg.Hint is null ? null : $" hint: {pg.Hint};",
                    pg.Where is null ? null : $" where: {pg.Where}");
                return (text, pg.Detail, 400);

            case NpgsqlException npgsql:
                return ($"{npgsql.GetType().Name}: {npgsql.Message}", null, 503);

            case MessageException message:
                return (message.Message, null, message.StatusCode);

            case OperationCanceledException:
                return ("Запит скасовано", null, 499);

            default:
                return ($"{ex.GetType().Name}: {ex.Message}", ex.ToString(), 500);
        }
    }
}

/// <summary>Ошибка, уже сформулированная для пользователя, вместе с кодом ответа.</summary>
public sealed class MessageException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
