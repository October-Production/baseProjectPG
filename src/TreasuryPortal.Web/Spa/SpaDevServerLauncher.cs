using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace TreasuryPortal.Web.Spa;

/// <summary>
/// ТОЛЬКО ДЛЯ ОТЛАДКИ (Development): на сервере не используется. Служба регистрируется, лишь
/// когда задан ключ Spa:DevServer:Url, а он есть только в appsettings.Development.json.
///
/// Поднимает ng serve вместе с приложением, чтобы хватало F5 или dotnet run.
/// Если порт уже слушается (ng serve запущен вручную или остался от прошлой отладки), используется он.
/// Запущенный здесь процесс гасится при штатной остановке; после «Stop debugging» он остаётся
/// и подхватывается следующим запуском.
/// </summary>
internal sealed class SpaDevServerLauncher(
    IOptions<SpaDevServerOptions> options,
    IHostEnvironment environment,
    ILogger<SpaDevServerLauncher> logger) : IHostedService, IDisposable
{
    private Process? _process;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.AutoStart || !Uri.TryCreate(settings.Url, UriKind.Absolute, out var url))
        {
            return;
        }

        if (await IsListeningAsync(url, cancellationToken))
        {
            logger.LogInformation("Angular dev server уже запущен: {Url}", url);
            return;
        }

        var workingDirectory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, settings.SourcePath));
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c {settings.Command}")
            : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", settings.Command } };
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.Environment["NG_CLI_ANALYTICS"] = "false"; // без интерактивного вопроса про аналитику

        logger.LogInformation("Запускаю «{Command}» в {Directory}", settings.Command, workingDirectory);
        Process? process;
        try
        {
            process = _process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось запустить «{Command}» — запустите ng serve вручную", settings.Command);
            return;
        }

        if (process is null)
        {
            logger.LogWarning("Не удалось запустить «{Command}» — запустите ng serve вручную", settings.Command);
            return;
        }

        // Ждём порт: сервер начнёт принимать запросы, когда клиент уже собран,
        // и браузер откроется на готовую страницу
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.StartTimeoutSeconds));
        try
        {
            while (!await IsListeningAsync(url, timeout.Token))
            {
                if (process.HasExited)
                {
                    logger.LogError(
                        "«{Command}» завершился с кодом {ExitCode}. Проверьте вывод выше и npm install в {Directory}",
                        settings.Command, process.ExitCode, workingDirectory);
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), timeout.Token);
            }

            logger.LogInformation("Angular dev server готов: {Url}. Открывайте адрес бэкенда, а не этот", url);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Angular dev server не ответил за {Seconds} с, продолжаю без ожидания",
                settings.StartTimeoutSeconds);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopDevServer();
        return Task.CompletedTask;
    }

    public void Dispose() => StopDevServer();

    private void StopDevServer()
    {
        var process = Interlocked.Exchange(ref _process, null);
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true); // cmd.exe -> node -> esbuild
            }
        }
        catch (Exception)
        {
            // уже завершился или нет прав — для dev-процесса неважно
        }
        finally
        {
            process.Dispose();
        }
    }

    private static async Task<bool> IsListeningAsync(Uri url, CancellationToken cancellationToken)
    {
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(url.Host, url.Port, probe.Token);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
