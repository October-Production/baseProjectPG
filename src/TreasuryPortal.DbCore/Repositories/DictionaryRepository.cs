using System.Text.Json;
using Npgsql;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Repositories.Abstract;

namespace TreasuryPortal.DbCore.Repositories;

/// <inheritdoc cref="IDictionaryRepository"/>
/// <remarks>
/// Образец репозитория сущности: смотрите на него, когда добавляете свою. Всё, что он делает, —
/// называет функцию и передаёт запрос; вызов, журнал и разбор ответа берёт на себя
/// <see cref="EntityRepositoryCore"/>.
/// </remarks>
internal sealed class DictionaryRepository(PgDataSource dataSource, ISystemLog log)
    : EntityRepositoryCore(dataSource, log, "dictionary"), IDictionaryRepository
{
    public Task<JsonElement> GetListAsync(
        IUserCredential user, string dictionaryName, CancellationToken cancellationToken = default) =>
        GetListAsync(
            user,
            request: new { name = dictionaryName },
            cancellationToken: cancellationToken);

    public Task<JsonElement> GetListsAsync(
        IUserCredential user, IEnumerable<string> dictionaryNames, CancellationToken cancellationToken = default) =>
        ReadAsync(
            user,
            Entity,
            "lists",
            function: "dictionary_lists",
            request: new { names = (dictionaryNames ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() },
            ReadItem,
            cancellationToken);
}
