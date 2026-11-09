using Npgsql;
using NpgsqlTypes;
using TreasuryPortal.DbCore.Log.Abstract;
using TreasuryPortal.DbCore.Models;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Repositories.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Repositories;

/// <summary>
/// Веб-роли и права из базы. Функции те же, что в BanknoteOps: пользователь целиком уходит
/// одним параметром jsonb, вместе с именами его AD-групп, а база возвращает массив имён.
///
/// Ошибки отсюда не заворачиваются в конверт, а летят наверх: вызывающая сторона
/// (Web/Auth/UserDirectory) должна отличить «прав нет» от «база недоступна» — иначе
/// одна заминка тихо оставила бы всех без доступа на время жизни кэша.
/// </summary>
internal sealed class AuthorizationRepository(NpgsqlDataSource dataSource, ISystemLog log)
    : PgRepositoryCore(dataSource, log), IAuthorizationRepository
{
    public Task<List<string>> GetRolesAsync(IUserCredential user, CancellationToken cancellationToken = default) =>
        ReadNamesAsync(user, "role", "get_user_roles", cancellationToken);

    public Task<List<string>> GetPermissionsAsync(IUserCredential user, CancellationToken cancellationToken = default) =>
        ReadNamesAsync(user, "permission", "get_user_permissions", cancellationToken);

    public async Task<UserAccess> GetUserAccessAsync(
        IUserCredential user, CancellationToken cancellationToken = default)
    {
        // Сначала роли: функция выдачи прав получает пользователя уже с ними
        var roles = await GetRolesAsync(user, cancellationToken);
        (user as UserCredential)?.SetRoles(roles);

        var permissions = await GetPermissionsAsync(user, cancellationToken);
        (user as UserCredential)?.SetPermissions(permissions);

        return new UserAccess(roles, permissions);
    }

    private async Task<List<string>> ReadNamesAsync(
        IUserCredential user, string entity, string function, CancellationToken cancellationToken)
    {
        using var logged = Log.Start(user, entity, "list");
        try
        {
            await using var command = DataSource.CreateCommand($"select {PgSchema.Name}.{function}(@usr);");
            command.Parameters.AddWithValue("@usr", NpgsqlDbType.Jsonb, user.ToJsonString());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await JsonMiddleWare.ReadStringListAsync(reader, cancellationToken);
        }
        catch (Exception ex)
        {
            logged.Fail(ex);
            throw;
        }
    }
}
