using System.Security.Claims;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.Utils;

namespace TreasuryPortal.DbCore.Models;

/// <inheritdoc cref="IUserCredential"/>
public sealed class UserCredential : IUserCredential
{
    public string Name { get; } = "";
    public string Login { get; } = "";
    public string DomainName { get; } = "";
    public string Host { get; } = "";
    public string? FullName { get; }
    public string? Mail { get; }
    public IReadOnlyList<string> Groups { get; } = [];
    public IReadOnlyList<string> Roles { get; private set; } = [];
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    /// <summary>Из Windows-идентичности: имя, домен и имена групп берутся из токена и AD.</summary>
    public UserCredential(ClaimsPrincipal claims, string? host, bool readGroups = true, bool readProfile = true)
    {
        var info = AdHelper.Instance.GetPrincipalInfo(claims, readGroups, readProfile);

        Name = info.Name;
        Login = info.Login;
        DomainName = info.Domain;
        Groups = info.GroupNames ?? [];
        FullName = info.FullName;
        Mail = info.Mail;
        Host = host ?? "";
    }

    /// <summary>Из готового имени: для фоновых задач и тестов, без обращения к AD.</summary>
    public UserCredential(string name, string? host = null, IReadOnlyList<string>? groups = null)
    {
        Name = name ?? "";
        Host = host ?? "";
        Groups = groups ?? [];

        var parts = Name.Split('\\', 2);
        if (parts.Length == 2)
        {
            DomainName = parts[0];
            Login = parts[1].ToLowerInvariant();
        }
        else
        {
            Login = Name.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Роли, выданные базой. Заполняются до запроса прав: функция выдачи прав получает
    /// пользователя целиком, вместе с уже известными ролями.
    /// </summary>
    public void SetRoles(IReadOnlyList<string> roles) => Roles = roles ?? [];

    public void SetPermissions(IReadOnlyList<string> permissions) => Permissions = permissions ?? [];
}
