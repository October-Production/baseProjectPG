using TreasuryPortal.DbCore.Models;
using TreasuryPortal.DbCore.Models.Abstract;

namespace TreasuryPortal.Web.Auth;

/// <summary>
/// Пользователь текущего запроса в том виде, в каком его принимает слой данных.
/// Эндпоинты берут его из DI и передают в репозиторий — HttpContext в DbCore не попадает.
///
/// Применение:  app.MapGet("/api/…", (CurrentUser me, ITreasuryUow uow) => uow.X.YAsync(me.Credential));
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor, UserDirectory directory)
{
    private IUserCredential? _credential;

    public IUserCredential Credential => _credential ??= Resolve();

    private IUserCredential Resolve()
    {
        var httpContext = accessor.HttpContext;
        return httpContext is null
            ? new UserCredential(string.Empty)
            : directory.GetCredential(httpContext.User, httpContext);
    }
}
