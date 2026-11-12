using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;
using TreasuryPortal.DbCore.Models;
using TreasuryPortal.DbCore.Repositories.Abstract;

namespace TreasuryPortal.DbCore.Repositories;

/// <summary>
/// Системный журнал в базе.:
/// csen.log_event(entity, op, json, user, err) возвращает log_id,
/// csen.log_event_end(id, json, err) закрывает запись.
///
/// Конец операции пишет тело запроса обёрнутым в { "root": … }, а текст ошибки кладёт
/// рядом ключом "error" — так же, как это делает BanknoteOps.
/// </summary>
internal sealed class LogRepository(PgDataSource dataSource) : ILogRepository
{
    public async Task LogStartAsync(LogData data)
    {
        await using var command = dataSource.Primary.CreateCommand(
            $"select {PgSchema.Name}.log_event(@entity, @op, @json, @user, @err);");

        command.Parameters.AddWithValue("@entity", data.Entity);
        command.Parameters.AddWithValue("@op", data.Operation);
        command.Parameters.AddWithValue("@json", NpgsqlDbType.Jsonb, data.Json);
        command.Parameters.AddWithValue("@user", data.UserLogin);
        command.Parameters.AddWithValue("@err", !string.IsNullOrEmpty(data.ErrorText));

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync() && !reader.IsDBNull(0))
        {
            data.LogId = reader.GetInt64(0);
        }
    }

    public async Task LogEndAsync(LogData data)
    {
        if (data.LogId == 0)
        {
            return;
        }

        var hasError = !string.IsNullOrEmpty(data.ErrorText);

        var payload = new JsonObject { ["root"] = ParseOrNull(data.Json) };
        if (hasError)
        {
            payload["error"] = data.ErrorText;
        }

        await using var command = dataSource.Primary.CreateCommand(
            $"select {PgSchema.Name}.log_event_end(@id, @json, @err);");

        command.Parameters.AddWithValue("@id", data.LogId);
        command.Parameters.AddWithValue("@json", NpgsqlDbType.Jsonb, payload.ToJsonString());
        command.Parameters.AddWithValue("@err", hasError);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Тело запроса могло не сериализоваться — журнал из-за этого падать не должен.</summary>
    private static JsonNode? ParseOrNull(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        }
        catch (Exception)
        {
            return JsonValue.Create(json);
        }
    }
}
