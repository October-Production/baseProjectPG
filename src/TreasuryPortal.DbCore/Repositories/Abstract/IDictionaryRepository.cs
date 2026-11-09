using System.Text.Json;
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.DbCore.Repositories.Abstract;

/// <summary>
/// Справочники портала: валюты, типы операций, статусы и всё, что экран показывает в выпадающих
/// списках. Один репозиторий на все справочники — они читаются по имени.
/// </summary>
public interface IDictionaryRepository
{
    /// <summary>Значения одного справочника: массив записей.</summary>
    Task<JsonElement> GetListAsync(
        IUserCredential user, string dictionaryName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Несколько справочников за один вызов: карточке сущности их нужно сразу три-четыре,
    /// и запрашивать каждый отдельно — это лишний round trip и лишняя строка в журнале.
    /// Возвращает объект, где ключ — имя справочника.
    /// </summary>
    Task<JsonElement> GetListsAsync(
        IUserCredential user, IEnumerable<string> dictionaryNames, CancellationToken cancellationToken = default);
}
