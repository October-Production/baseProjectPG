using System.Text.Json;
using Npgsql;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.DbCore.Repositories.Abstract;

/// <summary>
/// Соглашение, по которому живёт каждая сущность портала: список, одна запись со связями,
/// сохранение и удаление. Репозиторий сущности называет только функции и передаёт запрос —
/// техника вызова, журнал и разбор ответа описаны здесь и в <see cref="PgRepositoryCore"/>.
///
/// Имена функций складываются как &lt;сущность&gt;_&lt;операция&gt; в схеме csen, например
/// csen.payment_order_list. Если функция названа иначе, передайте её имя явно.
///
/// Новая сущность = интерфейс в Repositories/Abstract + класс здесь рядом + свойство в
/// <see cref="UOW.Abstract.ITreasuryUow"/>. Больше ничего в слое данных трогать не нужно.
/// </summary>
internal abstract class EntityRepositoryCore(PgDataSource dataSource, ISystemLog log, string entity)
    : PgRepositoryCore(dataSource, log)
{
    /// <summary>Имя сущности в журнале и префикс имён функций.</summary>
    private protected string Entity { get; } = entity;

    /// <summary>Список записей: csen.&lt;сущность&gt;_list(user, request).</summary>
    private protected Task<JsonElement> GetListAsync(
        IUserCredential user,
        object? request = null,
        string? function = null,
        CancellationToken cancellationToken = default) =>
        ReadAsync(user, Entity, "list", function ?? Function("list"), request, ReadList, cancellationToken);

    /// <summary>Одна запись со вложенными списками: csen.&lt;сущность&gt;_item(user, request).</summary>
    private protected Task<JsonElement> GetItemAsync(
        IUserCredential user,
        object request,
        string? function = null,
        CancellationToken cancellationToken = default) =>
        ReadAsync(user, Entity, "item", function ?? Function("item"), request, ReadItem, cancellationToken);

    /// <summary>Создание или изменение: csen.&lt;сущность&gt;_save(user, request).</summary>
    private protected Task<JsonElement> SaveAsync(
        IUserCredential user,
        object request,
        string? function = null,
        CancellationToken cancellationToken = default) =>
        WriteAsync(user, Entity, "save", function ?? Function("save"), request, ReadItem, cancellationToken);

    /// <summary>Удаление: csen.&lt;сущность&gt;_delete(user, request).</summary>
    private protected Task<JsonElement> DeleteAsync(
        IUserCredential user,
        object request,
        string? function = null,
        CancellationToken cancellationToken = default) =>
        WriteAsync(user, Entity, "delete", function ?? Function("delete"), request, ReadItem, cancellationToken);

    private string Function(string operation) => $"{Entity}_{operation}";
}
