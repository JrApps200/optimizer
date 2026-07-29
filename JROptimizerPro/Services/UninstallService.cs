using System.Diagnostics;
using System.Text.RegularExpressions;
using JROptimizerPro.Core;
using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class UninstallService
{
    private static readonly Regex MsiProductCode = new(@"\{[0-9A-Fa-f\-]{36}\}", RegexOptions.Compiled);

    public static async Task<IReadOnlyList<UninstallResult>> UninstallManyAsync(
        IReadOnlyList<InstalledApp> apps,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<UninstallResult>();
        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("Desinstalando " + app.Name + "...");
            var result = await UninstallAsync(app, cancellationToken);
            results.Add(result);
            AppLogger.Info($"Desinstalação {app.Name}: {(result.Success ? "sucesso" : "falha")} - {result.Message}");
        }

        return results;
    }

    public static async Task<UninstallResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default)
    {
        try
        {
            if (app.IsAppx)
                return await UninstallAppxAsync(app, cancellationToken);

            var command = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
                ? app.QuietUninstallString
                : app.UninstallString;

            if (string.IsNullOrWhiteSpace(command))
                return new UninstallResult(app, false, "Comando de desinstalação não encontrado.");

            if (command.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                var productCode = MsiProductCode.Match(command).Value;
                if (!string.IsNullOrWhiteSpace(productCode))
                {
                    var msiResult = await CommandService.RunAsync(
                        "msiexec.exe",
                        $"/x {productCode} /passive /norestart",
                        TimeSpan.FromMinutes(30),
                        cancellationToken);

                    var success = msiResult.ExitCode is 0 or 1605 or 3010;
                    return new UninstallResult(app, success,
                        success ? "Desinstalação MSI concluída." : $"MSI retornou código {msiResult.ExitCode}.");
                }
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                    Arguments = "/d /s /c " + command,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
                return new UninstallResult(app, false, "Não foi possível iniciar o desinstalador.");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                return new UninstallResult(app, false, "Desinstalador excedeu 30 minutos ou foi cancelado.");
            }

            var ok = process.ExitCode is 0 or 3010;
            return new UninstallResult(app, ok,
                ok ? "Desinstalador concluído." : $"Desinstalador retornou código {process.ExitCode}.");
        }
        catch (Exception ex)
        {
            return new UninstallResult(app, false, ex.Message);
        }
    }

    private static async Task<UninstallResult> UninstallAppxAsync(InstalledApp app, CancellationToken cancellationToken)
    {
        var escaped = app.PackageFullName.Replace("'", "''", StringComparison.Ordinal);
        var script = $"Remove-AppxPackage -Package '{escaped}' -ErrorAction Stop";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var result = await CommandService.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            TimeSpan.FromMinutes(10),
            cancellationToken);

        return new UninstallResult(
            app,
            result.Success,
            result.Success ? "Aplicativo da Microsoft Store removido." : result.CombinedOutput.Trim());
    }
}
