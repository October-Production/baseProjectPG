using Microsoft.AspNetCore.Mvc;
using TreasuryPortal.DbCore.UOW.Abstract;
using TreasuryPortal.Integrations.Services.B2;
using TreasuryPortal.Integrations.Services.B2.Abstract;
using TreasuryPortal.Web.Auth;
using TreasuryPortal.Web.Configuration.Abstract;
using TreasuryPortal.Web.Controllers.Base;

namespace TreasuryPortal.Web.Controllers;

/// <summary>
/// Угоди з B2. Образец контроллера над внешним сервисом: он только передаёт параметры. Вызов,
/// вход, журнал и перевод сбоя B2 в ответ 502 или 504 с понятным текстом берёт на себя
/// TreasuryPortal.Integrations.
///
/// В kaznacms здесь же проверялись роли и собирался JSON с полем error — теперь это делают
/// [AuthorizePerm] и ApiExceptionHandler.
/// </summary>
[Route("api/b2")]
[AuthorizePerm(AppPermissions.DealRead)]
public sealed class B2Controller(IB2Service b2, ITreasuryUow uow, ISettingsHolder settings, CurrentUser currentUser)
    : ControllerCore(uow, settings, currentUser)
{
    /// <summary>GET api/b2/deals?accountNumber=…&amp;currencyCode=… — угоди по рахунку і валюті.</summary>
    [HttpGet("deals")]
    public Task<IReadOnlyList<B2Deal>> SearchDealsAsync(
        [FromQuery] string accountNumber, [FromQuery] string currencyCode, CancellationToken cancellationToken) =>
        b2.SearchDealsAsync(Me, accountNumber.Trim(), currencyCode.Trim(), cancellationToken);
}
