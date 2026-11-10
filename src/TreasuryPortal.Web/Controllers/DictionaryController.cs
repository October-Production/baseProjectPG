using Microsoft.AspNetCore.Mvc;
using TreasuryPortal.DbCore.UOW.Abstract;
using TreasuryPortal.Web.Auth;
using TreasuryPortal.Web.Configuration.Abstract;
using TreasuryPortal.Web.Controllers.Base;

namespace TreasuryPortal.Web.Controllers;

/// <summary>
/// Справочники портала.
///
/// Образец контроллера сущности: копируйте его, когда добавляете свою. Он только называет
/// операцию репозитория — вызов, журнал и разбор ответа берут на себя слой данных
/// и <see cref="ControllerCore"/>.
/// </summary>
[Route("api/dictionaries")]
[AuthorizePerm(AppPermissions.DictionaryRead)]
public sealed class DictionaryController(ITreasuryUow uow, ISettingsHolder settings, CurrentUser currentUser)
    : ControllerCore(uow, settings, currentUser)
{
    /// <summary>GET api/dictionaries/{name} — значения одного справочника.</summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> GetListAsync(string name, CancellationToken cancellationToken) =>
        CheckResultForErrors(await Uow.Dictionaries.GetListAsync(Me, name, cancellationToken));

    /// <summary>
    /// GET api/dictionaries?names=currency&amp;names=operationType — несколько справочников
    /// одним запросом: карточке их нужно сразу несколько.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetListsAsync(
        [FromQuery] string[] names, CancellationToken cancellationToken) =>
        CheckResultForErrors(await Uow.Dictionaries.GetListsAsync(Me, names, cancellationToken));
}
