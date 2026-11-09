namespace TreasuryPortal.DbCore.Models;

/// <summary>
/// Что пользователю выдала база: веб-роли и права. Расширяется по мере добавления процессов —
/// список запрашиваемых имён задаёт приложение (Web/Auth/AppAccess.cs).
/// </summary>
public sealed record UserAccess(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public static readonly UserAccess Empty = new([], []);
}
