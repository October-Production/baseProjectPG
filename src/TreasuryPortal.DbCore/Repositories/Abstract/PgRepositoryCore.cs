using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Repositories.Abstract;

/// <summary>
/// Всё, что общего у репозиториев PostgreSQL: источник подключений, запись в системный журнал
/// и конверт ошибки, который понимает клиент.
///
/// Соглашение вызова то же, что в BanknoteOps: функция принимает пользователя и запрос двумя
/// параметрами jsonb и возвращает одно значение json. Никаких наборов строк, XML и построителей
/// документа — поэтому слой заметно короче версии под MS SQL.
/// </summary>
internal abstract class PgRepositoryCore(NpgsqlDataSource dataSource, ISystemLog log)
{
    private protected readonly NpgsqlDataSource DataSource = dataSource;
    private protected readonly ISystemLog Log = log;

    #region Ошибки

    public static JsonElement WrapError(string message, string? details = null) =>
        WrapError("server.api.exception", message, details);

    public static JsonElement WrapError(string code, string message, string? details)
    {
        var payload = new JsonObject
        {
            ["errors"] = new JsonArray(new JsonObject
            {
                ["type"] = "err",
                ["code"] = code,
                ["message"] = message,
                ["details"] = details,
            }),
        };

        using var document = JsonDocument.Parse(payload.ToJsonString());
        return document.RootElement.Clone();
    }

    public static JsonElement WrapError(Exception exception)
    {
        var (message, details, _) = Helper.GetExceptionText(exception);
        return WrapError(message, details);
    }

    #endregion

    #region Чтение и запись

    /// <summary>
    /// Выполняет функцию и отдаёт reader в <paramref name="readData"/>. Операция попадает
    /// в системный журнал от начала до конца, вместе с ошибкой, — но ни одна из этих записей
    /// не ждёт базу: они уходят в очередь фоновой службы.
    /// </summary>
    private protected async Task<JsonElement> ReadAsync(
        IUserCredential user,
        string entity,
        string operation,
        string function,
        object? request,
        Func<NpgsqlDataReader, CancellationToken, Task<JsonElement>> readData,
        CancellationToken cancellationToken = default)
    {
        using var logged = Log.Start(user, entity, operation, request);
        try
        {
            return await ExecuteAsync(user, function, request, readData, cancellationToken);
        }
        catch (Exception ex)
        {
            logged.Fail(ex);
            return WrapError(ex);
        }
    }

    /// <summary>
    /// То же, что <see cref="ReadAsync"/>, но сначала берётся общий замок записи: две правки
    /// одной сущности не идут одновременно.
    /// </summary>
    private protected async Task<JsonElement> WriteAsync(
        IUserCredential user,
        string entity,
        string operation,
        string function,
        object? request,
        Func<NpgsqlDataReader, CancellationToken, Task<JsonElement>> readData,
        CancellationToken cancellationToken = default)
    {
        using var logged = await Log.StartWriteAsync(user, entity, operation, request, cancellationToken);
        try
        {
            logged.EnsureWriteLock();
            return await ExecuteAsync(user, function, request, readData, cancellationToken);
        }
        catch (Exception ex)
        {
            logged.Fail(ex);
            return WrapError(ex);
        }
    }

    /// <summary>
    /// select csen.&lt;функция&gt;(@user_data, @request) — пользователь всегда первым параметром:
    /// по нему функция решает, что человеку видно и что ему можно.
    /// </summary>
    private async Task<JsonElement> ExecuteAsync(
        IUserCredential user,
        string function,
        object? request,
        Func<NpgsqlDataReader, CancellationToken, Task<JsonElement>> readData,
        CancellationToken cancellationToken)
    {
        await using var command = DataSource.CreateCommand(
            $"select {PgSchema.Name}.{function}(@user_data, @request);");

        command.Parameters.AddWithValue("@user_data", NpgsqlDbType.Jsonb, user.ToJsonString());
        command.Parameters.AddWithValue("@request", NpgsqlDbType.Jsonb, ToJson(request));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await readData(reader, cancellationToken);
    }

    /// <summary>Функция без параметров запроса получает пустой объект, а не NULL.</summary>
    private static string ToJson(object? request) => request switch
    {
        null => "{}",
        string text => string.IsNullOrWhiteSpace(text) ? "{}" : text,
        _ => request.ToJsonString(),
    };

    #endregion

    #region Разбор ответа

    private protected static Task<JsonElement> ReadItem(NpgsqlDataReader reader, CancellationToken cancellationToken) =>
        JsonMiddleWare.ReadItemAsync(reader, cancellationToken);

    private protected static Task<JsonElement> ReadList(NpgsqlDataReader reader, CancellationToken cancellationToken) =>
        JsonMiddleWare.ReadListAsync(reader, cancellationToken);

    #endregion
}
