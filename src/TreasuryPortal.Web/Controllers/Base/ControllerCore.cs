using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TreasuryPortal.DbCore.Models.Abstract;
using TreasuryPortal.DbCore.UOW.Abstract;
using TreasuryPortal.Web.Auth;
using TreasuryPortal.Web.Configuration.Abstract;

namespace TreasuryPortal.Web.Controllers.Base;

/// <summary>
/// Что нужно каждому контроллеру: единая точка входа в данные, настройки и учётные данные
/// текущего пользователя — то же, что _uow, _settings и GetUserCredentials() в ControllerCore
/// из BanknoteOps. Контроллер никогда не собирает строку подключения и не создаёт
/// репозиторий сам.
///
/// Новая сущность = контроллер отсюда + репозиторий в DbCore. Больше ничего в каркасе
/// трогать не нужно.
/// </summary>
[ApiController]
public abstract class ControllerCore(ITreasuryUow uow, ISettingsHolder settings, CurrentUser currentUser)
    : ControllerBase
{
    /// <summary>Единая точка входа в репозитории.</summary>
    private protected ITreasuryUow Uow { get; } = uow;

    /// <summary>
    /// Настройки приложения, в том числе пришедшие из Vault. Значения читаются в момент
    /// обращения, поэтому после смены значения в Vault здесь сразу новое.
    /// </summary>
    private protected ISettingsStore Settings { get; } = settings.Store;

    /// <summary>
    /// Пользователь запроса в том виде, в каком его принимает слой данных. Разрешается один раз
    /// на запрос и кэшируется, поэтому обращаться можно столько раз, сколько нужно.
    /// </summary>
    private protected IUserCredential Me => currentUser.Credential;

    /// <summary>
    /// Ответ функции наружу. Функции отвечают либо полезной нагрузкой, либо массивом «errors»,
    /// либо и тем и другим — частичный результат приходит вместе со своими предупреждениями.
    /// Настоящей ошибкой HTTP становится только ответ, в котором нет ничего кроме errors;
    /// иначе нагрузка идёт как есть, а клиент показывает ошибки рядом с ней.
    ///
    /// JSON отдаётся текстом как пришёл из PostgreSQL: имена свойств задаёт функция,
    /// и переименовывать их по правилам System.Text.Json нельзя.
    /// </summary>
    private protected IActionResult CheckResultForErrors(JsonElement payload)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return NoContent();
        }

        if (IsErrorsOnly(payload, out var message))
        {
            return Problem(detail: message, statusCode: StatusCodes.Status400BadRequest);
        }

        return Content(payload.GetRawText(), "application/json", System.Text.Encoding.UTF8);
    }

    private static bool IsErrorsOnly(JsonElement payload, out string message)
    {
        message = "";

        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("errors", out var errors) ||
            errors.ValueKind != JsonValueKind.Array ||
            errors.GetArrayLength() == 0)
        {
            return false;
        }

        foreach (var property in payload.EnumerateObject())
        {
            if (!property.NameEquals("errors"))
            {
                // Рядом с ошибками есть данные: это частичный результат, а не провал
                return false;
            }
        }

        foreach (var error in errors.EnumerateArray())
        {
            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var text) &&
                text.GetString() is { Length: > 0 } value)
            {
                message = value;
                break;
            }
        }

        if (message.Length == 0)
        {
            message = "Сталася помилка при обробці запиту сервером";
        }

        return true;
    }
}
