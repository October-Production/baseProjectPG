using System.Collections.Concurrent;
using System.DirectoryServices.AccountManagement;
using System.Runtime.Versioning;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TreasuryPortal.DbCore.Utils;

/// <summary>
/// Доменный пользователь так, как его видит приложение: имя, логин, имена AD-групп и профиль.
///
/// Имена групп нужны потому, что права выдаёт база по именам групп, а в Windows-токене лежат SID.
/// Трансляция SID → имя — это поход на контроллер домена, поэтому она кэшируется на весь процесс:
/// набор групп у банка меняется редко, а запрашивается на каждый вход пользователя.
/// </summary>
public sealed class AdHelper
{
    public static readonly AdHelper Instance = new();

    private readonly ConcurrentDictionary<(string Domain, string Sid), string> _groupNames = new();
    private readonly ConcurrentDictionary<(string Domain, string Login), AdUserProfile> _profiles = new();
    private readonly object _domainLock = new();

    private AdHelper()
    {
    }

    /// <param name="readGroups">Читать имена групп: нужно для выдачи прав, но стоит похода в AD.</param>
    /// <param name="readProfile">Читать ФИО, почту и табельный номер.</param>
    public AdPrincipalInfo GetPrincipalInfo(ClaimsPrincipal claims, bool readGroups, bool readProfile)
    {
#if DEBUG
        if (GetStubPrincipalInfo(readGroups, readProfile) is { } stub)
        {
            return stub;
        }
#endif

        var name = claims.Identity?.Name ?? "";

        // Вне Windows (тесты, Linux) групп токена нет: остаётся только имя
        return OperatingSystem.IsWindows()
            ? GetWindowsPrincipalInfo(claims, name, readGroups, readProfile)
            : new AdPrincipalInfo { Name = name };
    }

    [SupportedOSPlatform("windows")]
    private AdPrincipalInfo GetWindowsPrincipalInfo(
        ClaimsPrincipal claims, string name, bool readGroups, bool readProfile)
    {
        if (claims.Identities.OfType<WindowsIdentity>().FirstOrDefault() is not { } windows)
        {
            return new AdPrincipalInfo { Name = name };
        }

        var parts = name.Split('\\', 2);
        var domain = parts.Length == 2 ? parts[0] : "";
        var login = (parts.Length == 2 ? parts[1] : name).ToLowerInvariant();

        var profile = readProfile && domain.Length > 0 ? GetUserProfile(login, domain) : null;
        var groups = readGroups && domain.Length > 0
            ? GetGroupNames(windows.Groups?.Select(sid => sid.Value) ?? [], domain)
            : null;

        return new AdPrincipalInfo
        {
            Name = name,
            Login = login,
            Domain = domain,
            GroupNames = groups,
            FullName = profile?.FullName,
            Mail = profile?.Mail,
            EmployeeNumber = profile?.EmployeeNumber,
        };
    }

    /// <summary>Имена групп по их SID. Новые SID разрешаются одним подключением к домену.</summary>
    public List<string> GetGroupNames(IEnumerable<string> sids, string domain)
    {
        var names = new List<string>();
        List<string>? unresolved = null;

        foreach (var sid in sids)
        {
            if (_groupNames.TryGetValue((domain, sid), out var cached))
            {
                if (cached.Length > 0)
                {
                    names.Add(cached);
                }
            }
            else
            {
                (unresolved ??= []).Add(sid);
            }
        }

        if (unresolved is null || !OperatingSystem.IsWindows())
        {
            return names;
        }

        // Одно подключение к домену на всю пачку, и только одно одновременно на процесс
        lock (_domainLock)
        {
            ResolveGroupNames(unresolved, domain, names);
        }

        return names;
    }

    [SupportedOSPlatform("windows")]
    private void ResolveGroupNames(List<string> sids, string domain, List<string> names)
    {
        PrincipalContext? context = null;
        try
        {
            foreach (var sid in sids)
            {
                if (_groupNames.TryGetValue((domain, sid), out var cached))
                {
                    if (cached.Length > 0)
                    {
                        names.Add(cached);
                    }

                    continue;
                }

                var name = "";
                try
                {
                    context ??= new PrincipalContext(ContextType.Domain, domain);
                    using var group = GroupPrincipal.FindByIdentity(context, IdentityType.Sid, sid);
                    name = group?.Name ?? "";
                }
                catch (Exception)
                {
                    // Группа не из этого домена или DC недоступен: пропускаем, не кэшируя пустоту
                    continue;
                }

                _groupNames[(domain, sid)] = name;
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
        }
        finally
        {
            context?.Dispose();
        }
    }

    /// <summary>ФИО, почта и табельный номер из AD.</summary>
    public AdUserProfile GetUserProfile(string login, string domain)
    {
        if (_profiles.TryGetValue((domain, login), out var cached))
        {
            return cached;
        }

        var profile = new AdUserProfile { Login = login };

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var context = new PrincipalContext(ContextType.Domain, domain);
                using var user = UserPrincipal.FindByIdentity(context, IdentityType.SamAccountName, login);
                if (user is not null)
                {
                    profile = new AdUserProfile
                    {
                        Login = login,
                        FullName = user.DisplayName,
                        Mail = user.UserPrincipalName,
                        EmployeeNumber = user.EmployeeId,
                    };
                }
            }
            catch (Exception)
            {
                // DC недоступен: профиль не обязателен, приложению хватает имени и групп
            }
        }

        _profiles[(domain, login)] = profile;
        return profile;
    }

#if DEBUG
    /// <summary>
    /// ТОЛЬКО ДЛЯ ОТЛАДКИ (Debug): в Release-сборке этого метода нет.
    ///
    /// Отладка без домена: если рядом с приложением лежит user_stub.json, пользователь берётся
    /// оттуда. Файл перечитывается раз в 10 секунд, так что личность можно менять на лету.
    /// Сам файл — в .gitignore.
    /// </summary>
    private static AdPrincipalInfo? GetStubPrincipalInfo(bool readGroups, bool readProfile)
    {
        var item = UserStub.Current()?.Active();
        if (item?.Login is null || item.Domain is null)
        {
            return null;
        }

        return new AdPrincipalInfo
        {
            Name = $"{item.Domain}\\{item.Login}",
            Login = item.Login.ToLowerInvariant(),
            Domain = item.Domain,
            GroupNames = readGroups ? item.Groups ?? [] : null,
            FullName = readProfile ? item.FullName : null,
            Mail = readProfile ? item.Mail : null,
            EmployeeNumber = readProfile ? item.EmployeeNumber : null,
        };
    }
#endif
}

public sealed class AdPrincipalInfo
{
    public string Name { get; init; } = "";
    public string Login { get; init; } = "";
    public string Domain { get; init; } = "";

    /// <summary>null — имена групп не запрашивали; пустой список — пользователь ни в одной группе.</summary>
    public List<string>? GroupNames { get; init; }

    public string? FullName { get; init; }
    public string? Mail { get; init; }
    public string? EmployeeNumber { get; init; }
}

public sealed class AdUserProfile
{
    public string Login { get; init; } = "";
    public string? FullName { get; init; }
    public string? Mail { get; init; }
    public string? EmployeeNumber { get; init; }
}

#if DEBUG
/// <summary>
/// ТОЛЬКО ДЛЯ ОТЛАДКИ (Debug): в Release-сборке этого класса нет.
///
/// Содержимое user_stub.json: набор личностей и ключ активной.
/// </summary>
internal sealed class UserStub
{
    private static readonly object Sync = new();
    private static UserStub? _current;
    private static DateTime _readAt;

    [JsonPropertyName("use")]
    public string? Use { get; set; }

    [JsonPropertyName("users")]
    public List<UserStubUser>? Users { get; set; }

    public static UserStub? Current()
    {
        if (_current is not null && _readAt.AddSeconds(10) > DateTime.UtcNow)
        {
            return _current;
        }

        lock (Sync)
        {
            if (_current is not null && _readAt.AddSeconds(10) > DateTime.UtcNow)
            {
                return _current;
            }

            _current = Read();
            _readAt = DateTime.UtcNow;
            return _current;
        }
    }

    public UserStubUser? Active() =>
        Use is null || Users is null ? null : Users.Find(user => user.Key == Use);

    private static UserStub Read()
    {
        try
        {
            return File.Exists("user_stub.json")
                ? JsonSerializer.Deserialize<UserStub>(File.ReadAllText("user_stub.json")) ?? new UserStub()
                : new UserStub();
        }
        catch (Exception)
        {
            return new UserStub();
        }
    }
}

/// <summary>ТОЛЬКО ДЛЯ ОТЛАДКИ (Debug): одна личность из user_stub.json.</summary>
internal sealed class UserStubUser
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("login")]
    public string? Login { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("employeeNumber")]
    public string? EmployeeNumber { get; set; }

    [JsonPropertyName("groups")]
    public List<string>? Groups { get; set; }
}
#endif
