using System.Text.Json;
using JROptimizerPro.Core;
using JROptimizerPro.Models;
using Microsoft.Win32;

namespace JROptimizerPro.Services;

internal static class InstalledAppsService
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static async Task<List<InstalledApp>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            var apps = new List<InstalledApp>();
            ReadRegistryApps(RegistryHive.LocalMachine, RegistryView.Registry64, apps);
            ReadRegistryApps(RegistryHive.LocalMachine, RegistryView.Registry32, apps);
            ReadRegistryApps(RegistryHive.CurrentUser, RegistryView.Default, apps);

            try
            {
                apps.AddRange(await ReadAppxAppsAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                AppLogger.Warning("Não foi possível listar aplicativos da Microsoft Store: " + ex.Message);
            }

            return apps
                .Where(app => !string.IsNullOrWhiteSpace(app.Name))
                .GroupBy(app => $"{app.Name}|{app.Version}|{app.TypeText}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    private static void ReadRegistryApps(RegistryHive hive, RegistryView view, ICollection<InstalledApp> apps)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallKey);
            if (uninstall is null)
                return;

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var key = uninstall.OpenSubKey(subKeyName);
                    if (key is null)
                        continue;

                    var name = key.GetValue("DisplayName")?.ToString()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (Convert.ToInt32(key.GetValue("SystemComponent") ?? 0) == 1)
                        continue;

                    var releaseType = key.GetValue("ReleaseType")?.ToString() ?? string.Empty;
                    if (releaseType.Contains("Update", StringComparison.OrdinalIgnoreCase)
                        || releaseType.Contains("Hotfix", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var uninstallString = key.GetValue("UninstallString")?.ToString() ?? string.Empty;
                    var quietUninstallString = key.GetValue("QuietUninstallString")?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(uninstallString) && string.IsNullOrWhiteSpace(quietUninstallString))
                        continue;

                    apps.Add(new InstalledApp
                    {
                        Id = $"reg:{hive}:{view}:{subKeyName}",
                        Name = name,
                        Version = key.GetValue("DisplayVersion")?.ToString() ?? string.Empty,
                        Publisher = key.GetValue("Publisher")?.ToString() ?? string.Empty,
                        InstallLocation = key.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                        UninstallString = uninstallString,
                        QuietUninstallString = quietUninstallString,
                        IsAppx = false
                    });
                }
                catch
                {
                    // Ignora uma entrada corrompida e continua.
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Falha lendo aplicativos em {hive}/{view}: {ex.Message}");
        }
    }

    private static async Task<List<InstalledApp>> ReadAppxAppsAsync(CancellationToken cancellationToken)
    {
        const string script = "@((Get-AppxPackage | Where-Object { -not $_.IsFramework -and -not $_.NonRemovable } | Select-Object Name,PackageFullName,Publisher,Version)) | ConvertTo-Json -Compress";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var result = await CommandService.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            TimeSpan.FromMinutes(2),
            cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            return new List<InstalledApp>();

        using var document = JsonDocument.Parse(result.Output);
        var items = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToArray()
            : new[] { document.RootElement };

        var apps = new List<InstalledApp>();
        foreach (var item in items)
        {
            var name = ReadJsonString(item, "Name");
            var packageFullName = ReadJsonString(item, "PackageFullName");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(packageFullName))
                continue;

            apps.Add(new InstalledApp
            {
                Id = "appx:" + packageFullName,
                Name = name,
                Version = ReadJsonString(item, "Version"),
                Publisher = ReadJsonString(item, "Publisher"),
                IsAppx = true,
                PackageFullName = packageFullName
            });
        }

        return apps;
    }

    private static string ReadJsonString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) ? value.ToString() : string.Empty;
    }
}
