using System.Data.Common;
using System.Text.Json;

namespace TreasuryPortal.DbCore.Utils;

/// <summary>
/// Чтение ответа PostgreSQL.
/// </summary>
public static class JsonMiddleWare
{
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();
    private static readonly JsonElement EmptyArray = JsonDocument.Parse("[]").RootElement.Clone();

    /// <summary>Один объект: карточка сущности, результат сохранения.</summary>
    public static async Task<JsonElement> ReadItemAsync(DbDataReader reader, CancellationToken cancellationToken = default)
    {
        if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
        {
            return reader.GetFieldValue<JsonElement>(0).Clone();
        }

        return EmptyObject;
    }

    /// <summary>Список: массив записей.</summary>
    public static async Task<JsonElement> ReadListAsync(DbDataReader reader, CancellationToken cancellationToken = default)
    {
        if (await reader.ReadAsync(cancellationToken) && !reader.IsDBNull(0))
        {
            return reader.GetFieldValue<JsonElement>(0).Clone();
        }

        return EmptyArray;
    }

    /// <summary>Массив строк — так приходят роли и права пользователя.</summary>
    public static async Task<List<string>> ReadStringListAsync(
        DbDataReader reader, CancellationToken cancellationToken = default)
    {
        var json = await ReadListAsync(reader, cancellationToken);
        if (json.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>(json.GetArrayLength());
        foreach (var item in json.EnumerateArray())
        {
            // Функция может отдавать как ["name"], так и [{"name":"…"}] — принимаем оба вида
            var value = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("name", out var name) => name.GetString(),
                _ => null,
            };

            if (!string.IsNullOrEmpty(value))
            {
                result.Add(value);
            }
        }

        return result;
    }
}
