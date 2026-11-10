using System.Text.Json;
using System.Text.Json.Serialization;
using TreasuryPortal.DbCore;
using TreasuryPortal.Web.Auth;
using TreasuryPortal.Web.Configuration;
using TreasuryPortal.Web.Configuration.Abstract;
using TreasuryPortal.Web.Http;
using TreasuryPortal.Web.Hubs;
using TreasuryPortal.Web.Spa;

var builder = WebApplication.CreateBuilder(args);

// Как в BanknoteOps: Vault подмешивает значения в конфигурацию, а SettingsHolder отдаёт их
// коду типизированными. Контроллеры получают их через ControllerCore.Settings.
// Vault работает везде, включая отладку: на машине разработчика переменные окружения
// дают доступ к dev-разделу, где лежат все настройки
builder.Configuration.VaultInit();
builder.Services.AddSingleton<ISettingsHolder, SettingsHolder>();

// Слой данных: репозитории, системный журнал и его фоновая запись
builder.Services.AddDbCore(builder.Configuration);

// Доменная аутентификация, веб-роли и права из базы
builder.Services.AddTreasuryAuthentication(builder.Configuration);

// Связь с браузером в реальном времени: хаб SignalR и публикация сообщений
builder.Services.AddTreasuryRealtime();

// Перечисления ездят строками ("warning"), а не числами: так их читает TypeScript.
// Настроить надо оба конвейера — у контроллеров свои параметры сериализации, у минимальных
// эндпоинтов (/health, ответы обработчика ошибок) свои, и общими они не становятся.
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(StringEnums()));

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(StringEnums()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.AddSpaHosting();

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseApiProtection();

app.UseAuthentication();
app.UseAuthorization();

// Балансеру и мониторингу Windows-аутентификация недоступна
app.MapHealthChecks("/health").AllowAnonymous();

// ТОЛЬКО ДЛЯ ОТЛАДКИ: описание API по адресу /openapi/v1.json. На сервере не публикуется
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// --- Эндпоинты ---------------------------------------------------------------------------
// Новая сущность добавляется одним контроллером в Controllers/ — регистрировать его здесь
// не нужно, MapControllers находит его сам
app.MapControllers();

// Хаб лежит вне /api, поэтому регистрируется отдельно
app.MapTreasuryHubs();

// Неизвестный адрес API — 404, а не index.html из SPA-fallback
app.Map("/api/{**path}", () => TypedResults.NotFound());

app.MapSpa();

app.Run();

static JsonStringEnumConverter StringEnums() => new(JsonNamingPolicy.CamelCase);
