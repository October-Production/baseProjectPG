using System.Text.Json.Serialization;

namespace TreasuryPortal.DbCore.Models.Abstract;

/// <summary>
/// Кто выполняет операцию. Репозитории и журнал принимают только это — ни HttpContext,
/// ни ClaimsPrincipal в слой данных не попадают.
///
/// Этот объект целиком уходит в PostgreSQL первым параметром каждой функции (jsonb), поэтому
/// имена свойств закреплены атрибутами: переименование поля здесь ломает функции в базе.
/// </summary>
public interface IUserCredential
{
    /// <summary>DOMAIN\login — в таком виде имя пишется в журнал и в функции.</summary>
    [JsonPropertyName("name")]
    string Name { get; }

    [JsonPropertyName("login")]
    string Login { get; }

    [JsonPropertyName("domainName")]
    string DomainName { get; }

    /// <summary>Машина, с которой пришёл запрос: пишется в системный журнал.</summary>
    [JsonPropertyName("host")]
    string Host { get; }

    [JsonPropertyName("fio")]
    string? FullName { get; }

    [JsonPropertyName("mail")]
    string? Mail { get; }

    /// <summary>Имена AD-групп пользователя. По ним база выдаёт веб-роли и права.</summary>
    [JsonPropertyName("groups")]
    IReadOnlyList<string> Groups { get; }

    /// <summary>Роли, которые уже выдала база. Пустые, пока их не запросили.</summary>
    [JsonPropertyName("roles")]
    IReadOnlyList<string> Roles { get; }

    /// <summary>Права, которые уже выдала база. Пустые, пока их не запросили.</summary>
    [JsonPropertyName("permissions")]
    IReadOnlyList<string> Permissions { get; }
}
