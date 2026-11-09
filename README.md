# Treasury Portal

Внутренний портал казначейства Sense Bank. ASP.NET Core 10 раздаёт Angular 22 с того же origin,
пользователь определяется доменной аутентификацией (Negotiate: Kerberos с запасным NTLM), роли
и права приходят из базы PostgreSQL, системный журнал пишется фоновой службой.

Это каркас: бизнес-логики в нём нет, он сделан так, чтобы сущности и процессы добавлялись
поверх. Как он устроен и куда что класть — **[ARCHITECTURE.md](ARCHITECTURE.md)**.

```
src/TreasuryPortal.Web/      API, аутентификация, права, хаб SignalR, хостинг SPA
src/TreasuryPortal.DbCore/   репозитории, UOW, системный журнал (PostgreSQL)
src/TreasuryPortal.Client/   Angular 22 + Angular Material (тема Sense Bank)
deploy/                      создание сайта IIS с Windows-аутентификацией
db/                          что должно быть в базе
```

## Запуск локально

Нужны .NET SDK 10 и Node.js 22+.

1. Один раз: `dotnet dev-certs https --trust`.
2. Положите строку подключения в `src/TreasuryPortal.Web/appsettings.Local.json` (файл
   в `.gitignore`):

```json
{ "Configuration": { "ConnectionStrings": { "PGSQLServer": "Host=...;Database=...;Username=...;Password=..." } } }
```

3. В Visual Studio — F5 (профиль `https` или `IIS Express`), или в терминале:

```bash
dotnet run --project src/TreasuryPortal.Web --launch-profile https
```

4. Откройте **https://localhost:7210** — адрес бэкенда, а не `:4200`.

Первая Debug-сборка сама сделает `npm install` в клиенте, а приложение само поднимет `ng serve`.

Клиент подключён к решению как `TreasuryPortal.Client.esproj`, поэтому Visual Studio видит его
в обозревателе решений и умеет запускать (`npm start`). Собирать его сам esproj не пытается —
это делают цели в `TreasuryPortal.Web.csproj`, иначе клиент собирался бы дважды. Запускать
по-прежнему нужно `TreasuryPortal.Web` — он и поднимет `ng serve`.

В Development браузер ходит только на Kestrel или IIS Express: они проводят Windows-аутентификацию
сами, а всё, что не `api/` и не `hubs/`, проксируют на `ng serve` вместе с HMR (этим занимается
`UseProxyToSpaDevelopmentServer` из `Microsoft.AspNetCore.SpaServices.Extensions` — тот же
пакет и тот же вызов, что в NBC). Обратная схема — `ng serve`
с `proxy.conf` на API — ломает NTLM: прокси не держит соединения 1:1, шаги handshake уходят
в разные сокеты бэкенда, и сервер отвечает 400.

**Без базы и без домена.** Секция `Auth:Dev` в `appsettings.Development.json` подменяет роли
и права — в шапке тогда горит метка `DEV`. Удалите секцию, чтобы проверить настоящую выдачу.
Подменить саму личность можно файлом `user_stub.json` рядом с приложением (только Debug):

```json
{
  "use": "treasurer",
  "users": [
    { "key": "treasurer", "domain": "SENSE", "login": "ivanov", "fullName": "Іванов І.І.",
      "groups": ["TreasuryPortal-Users"] }
  ]
}
```

Тесты клиента: `cd src/TreasuryPortal.Client && npm test`.

## Публикация на IIS

На сервере нужны роль IIS «Windows Authentication» (`Install-WindowsFeature Web-Windows-Auth`)
и ASP.NET Core 10 Hosting Bundle.

```powershell
dotnet publish src/TreasuryPortal.Web -c Release -o publish
```

Release-сборка требует внутренний пакет `Util.VaultCoreLib` — это намеренно: публиковать сборку,
в которой Vault ничего не делает, нельзя. Если фида на машине нет, а собрать нужно (например,
чтобы проверить публикацию локально), добавьте `-p:UseVault=false` — тогда подставится заглушка,
и строку подключения придётся задавать настройками.

```powershell
# на сервере, Windows PowerShell 5.1 от администратора
.\deploy\Install-IisSite.ps1 -PhysicalPath 'D:\Sites\TreasuryPortal' -HostName 'treasury.sense.bank.int' -CertificateThumbprint '<отпечаток>'
```

Затем скопировать содержимое `publish` в `D:\Sites\TreasuryPortal`. Если сайт уже работает,
сначала положите в папку `app_offline.htm` — иначе DLL будут заняты процессом.

`dotnet publish` сам выполнит `npm ci` и `ng build` и положит клиент в `wwwroot`. Если CI собирает
клиент отдельным шагом: `-p:BuildSpa=false`, а `dist` положить в `wwwroot` самостоятельно.

Скрипт создаёт пул без managed-кода и сайт, выключает Anonymous и включает Windows-аутентификацию,
а `/health` оставляет анонимным. Аутентификация пишется в `applicationHost.config`: в `web.config`
эти секции по умолчанию заблокированы и дают ошибку 500.19.

Виртуальное приложение (`https://host/TreasuryPortal/`) работает без пересборки: сервер
подставляет `base href` при отдаче `index.html`. Для него те же настройки аутентификации нужно
применить к `Сайт/TreasuryPortal`.

Ещё на сервере стоит сделать:

- **Переменные окружения для Vault** у учётки пула: логин и пароль LDAP, которыми приложение
  забирает секреты.
- **Отключить HTTP/2** для привязки https (Bindings → Disable HTTP/2, Windows Server 2022+).
  Windows-аутентификация работает только поверх HTTP/1.1, и иначе браузер переключается
  посреди залпа запросов.
- **Включить тихий SSO.** Хост добавляется в политику `AuthServerAllowlist` для Chrome/Edge
  через GPO или в зону Local Intranet — тогда окна логина нет совсем.

## Kerberos вместо NTLM

NTLM делает трёхшаговый handshake на каждом новом соединении. Kerberos обычно укладывается
в один шаг и не требует, чтобы шаги шли по одному соединению. Каким протоколом прошёл вход,
видно на главной странице портала.

- Пользователи заходят по FQDN (`treasury.sense.bank.int`), а не по IP или короткому имени.
- SPN: `setspn -S HTTP/treasury.sense.bank.int <учётка>`. Для пула под ApplicationPoolIdentity
  учётка — это компьютер сервера (`SERVER$`). Для пула под доменной учёткой — она сама,
  и скрипт запускается с `-UseAppPoolCredentials`.
- Проверка в DevTools: заголовок `Authorization: Negotiate YII…` — Kerberos,
  `Negotiate TlRMTVNT…` — NTLM.

## Если что-то не так

| Симптом | Причина |
|---|---|
| 500.19 после правки `web.config` | В него добавили секции аутентификации, а на сервере они заблокированы. Настраивайте через скрипт. |
| `InvalidOperationException: The Negotiate Authentication handler cannot be used on a server that directly supports Windows Authentication` | В IIS или IIS Express выключена Windows-аутентификация. |
| Окно логина в Chrome или Edge | Хост не в зоне Local Intranet и не в `AuthServerAllowlist`. |
| Пользователь видит «Немає доступу» | Ни одна его AD-группа не выдаёт роль портала, либо имена ролей не заведены в базе. |
| `Не задана строка подключения Configuration:ConnectionStrings:PGSQLServer` | Нет `appsettings.Local.json` локально или не отработал Vault на сервере. Портал при этом стартует: интерфейс работает, данные — нет. |
| Красная точка в шапке, в консоли `Failed to start the connection` | Не поднялся хаб. На IIS проверьте, что для сайта включён WebSocket Protocol; портал при этом работает, просто без мгновенных обновлений. |
| В обозревателе решений нет клиента | Открыт не `TreasuryPortal.slnx`, или у Visual Studio не установлена рабочая нагрузка «ASP.NET and web development» — без неё SDK для `.esproj` не ставится. |
| В логе «Журнал операций не успевает: отброшено N записей» | База недоступна или не справляется. Пользователю это не мешает — журнал теряется намеренно. |
| 502 на `https://localhost:7210` | Не запустился `ng serve`. Ошибка — в консоли приложения. |
| `restore` падает на `Util.VaultCoreLib` | Нет внутреннего nuget-фида. Должны присутствовать `Directory.Build.targets` и `local-debug/`. |
