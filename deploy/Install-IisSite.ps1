#Requires -RunAsAdministrator
#Requires -PSEdition Desktop
<#
.SYNOPSIS
    Создаёт или обновляет сайт IIS для Treasury Portal с Windows-аутентификацией.

.DESCRIPTION
    Запускать в Windows PowerShell 5.1 (powershell.exe) от администратора: диск IIS:\ модуля
    WebAdministration в PowerShell 7 не работает.

    Скрипт идемпотентный: пул и сайт создаются, если их нет, настройки применяются при каждом запуске.

    Аутентификация пишется в applicationHost.config (location = сайт), а не в web.config приложения:
    секции system.webServer/security/authentication на сервере по умолчанию заблокированы,
    и в web.config дают ошибку 500.19.

    Что настраивается:
      - пул без managed-кода (ASP.NET Core in-process), AlwaysRunning, без idle-таймаута;
      - Anonymous выкл., Windows вкл.: под аутентификацией и index.html, и статика, поэтому первый
        handshake проходит загрузка самой страницы, ещё до старта Angular;
      - /health анонимно: балансер и мониторинг Windows-аутентификацию не проходят.

.PARAMETER UseAppPoolCredentials
    Для Kerberos, когда пул работает под доменной учёткой и SPN HTTP/<host> зарегистрирован на неё.

.EXAMPLE
    .\Install-IisSite.ps1 -PhysicalPath 'D:\Sites\TreasuryPortal'

.EXAMPLE
    .\Install-IisSite.ps1 -PhysicalPath 'D:\Sites\TreasuryPortal' -HostName 'treasury.sense.bank.int' -CertificateThumbprint 'A1B2C3D4...'
#>
[CmdletBinding()]
param(
    [string] $SiteName = 'TreasuryPortal',

    [Parameter(Mandatory)]
    [string] $PhysicalPath,

    [string] $AppPoolName = $SiteName,

    # Пусто - сайт отвечает на любое имя (порт не должен быть занят другим сайтом)
    [string] $HostName = '',

    # Отпечаток сертификата из LocalMachine\My. Пусто - HTTP-привязка, только для проверки
    [string] $CertificateThumbprint = '',

    # 0 - 443 для HTTPS, 80 для HTTP
    [int] $Port = 0,

    [switch] $UseAppPoolCredentials
)

$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

# --- Предпосылки ---------------------------------------------------------------
$modules = Get-WebGlobalModule | Select-Object -ExpandProperty Name
if ($modules -notcontains 'AspNetCoreModuleV2') {
    throw 'В IIS нет модуля AspNetCoreModuleV2: установите ASP.NET Core 10 Hosting Bundle и выполните iisreset.'
}
if ($modules -notcontains 'WindowsAuthenticationModule') {
    throw 'В IIS нет Windows-аутентификации: Install-WindowsFeature Web-Windows-Auth (на клиентской Windows - компонент IIS "Windows Authentication").'
}

$useHttps = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)
if ($Port -eq 0) {
    if ($useHttps) { $Port = 443 } else { $Port = 80 }
}

# --- Пул -----------------------------------------------------------------------
$poolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path $poolPath)) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "Создан пул $AppPoolName"
}
Set-ItemProperty $poolPath -Name managedRuntimeVersion -Value ''
Set-ItemProperty $poolPath -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty $poolPath -Name processModel.idleTimeout -Value '00:00:00'

# --- Сайт ----------------------------------------------------------------------
if (-not (Test-Path $PhysicalPath)) {
    New-Item -ItemType Directory -Path $PhysicalPath | Out-Null
}

$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path $sitePath)) {
    $sslFlags = 0
    if ($useHttps -and $HostName) { $sslFlags = 1 }   # SNI

    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName `
        -Port $Port -HostHeader $HostName -Ssl:$useHttps -SslFlags $sslFlags | Out-Null

    if ($useHttps) {
        $binding = Get-WebBinding -Name $SiteName -Protocol 'https' -Port $Port
        $binding.AddSslCertificate($CertificateThumbprint, 'My')
    }
    Write-Host "Создан сайт $SiteName -> $PhysicalPath"
}
else {
    Set-ItemProperty $sitePath -Name applicationPool -Value $AppPoolName
    Set-ItemProperty $sitePath -Name physicalPath -Value $PhysicalPath
    Write-Host "Сайт $SiteName уже есть: обновлены пул и путь, привязки не тронуты"
}

# --- Аутентификация -------------------------------------------------------------
$apphost = 'MACHINE/WEBROOT/APPHOST'
$auth = 'system.webServer/security/authentication'

Set-WebConfigurationProperty -PSPath $apphost -Location $SiteName -Filter "$auth/anonymousAuthentication" -Name enabled -Value $false
Set-WebConfigurationProperty -PSPath $apphost -Location $SiteName -Filter "$auth/windowsAuthentication" -Name enabled -Value $true
Set-WebConfigurationProperty -PSPath $apphost -Location $SiteName -Filter "$auth/windowsAuthentication" -Name useAppPoolCredentials -Value ([bool]$UseAppPoolCredentials)

Set-WebConfigurationProperty -PSPath $apphost -Location "$SiteName/health" -Filter "$auth/anonymousAuthentication" -Name enabled -Value $true

$scheme = if ($useHttps) { 'https' } else { 'http' }
$hostPart = if ($HostName) { $HostName } else { 'localhost' }
Write-Host ''
Write-Host "Готово: $($scheme)://$($hostPart):$Port/"
Write-Host "Дальше: скопируйте результат dotnet publish в $PhysicalPath."
Write-Host 'Если сайт уже работает, сначала положите в папку app_offline.htm - иначе DLL будут заняты процессом.'
Write-Host ''
Write-Host 'Не забудьте: переменные окружения VAULT_LDAP_USER / VAULT_LDAP_PASSWORD для учётки пула,'
Write-Host 'отключённый HTTP/2 на https-привязке и хост в AuthServerAllowlist для тихого SSO.'
