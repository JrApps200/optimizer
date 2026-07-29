using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using JROptimizerPro.Core;
using JROptimizerPro.Models;
using Microsoft.Win32;

namespace JROptimizerPro.Services;

internal static class OptimizationService
{
    private const string AutoStartTaskName = "JR Optimizer Pro AutoStart";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Task<OptimizationResult> ApplyAsync(
        OptimizationOptions options,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Apply(options, cancellationToken), cancellationToken);

    public static Task<OptimizationResult> RestoreAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Restore(cancellationToken), cancellationToken);

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{AutoStartTaskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(10_000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    public static string SetAutoStart(bool enabled)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
            return "Não foi possível identificar o executável atual.";

        var arguments = enabled
            ? $"/Create /TN \"{AutoStartTaskName}\" /TR \"\\\"{executable}\\\"\" /SC ONLOGON /RL HIGHEST /F"
            : $"/Delete /TN \"{AutoStartTaskName}\" /F";

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(15_000);
            if (process is { ExitCode: 0 })
                return enabled ? "Inicialização automática ativada." : "Inicialização automática desativada.";

            return process?.StandardError.ReadToEnd().Trim() ?? "Falha ao alterar a tarefa agendada.";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static OptimizationResult Apply(OptimizationOptions options, CancellationToken cancellationToken)
    {
        var changes = new List<string>();
        var errors = new List<string>();
        var backup = LoadOrCreateBackup();

        if (options.DisableTransparency)
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, RegistryValueKind.DWord, "Transparência desativada", changes, errors);

        if (options.ReduceAnimations)
        {
            SetRegistry(backup, RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String, "Animações de janelas reduzidas", changes, errors);
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, RegistryValueKind.DWord, "Efeitos visuais priorizam desempenho", changes, errors);
        }

        if (options.DisableGameDvr)
        {
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord, "Captura em segundo plano desativada", changes, errors);
            SetRegistry(backup, RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord, "Game DVR desativado", changes, errors);
        }

        if (options.DisableBackgroundApps)
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, RegistryValueKind.DWord, "Aplicativos em segundo plano restringidos", changes, errors);

        if (options.DisableWebSearch)
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord, "Resultados da web no menu Iniciar desativados", changes, errors);

        if (options.DisableSuggestions)
        {
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord, "Sugestões do Windows reduzidas", changes, errors);
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0, RegistryValueKind.DWord, "Instalação silenciosa de sugestões desativada", changes, errors);
        }

        if (options.DisableWidgets)
            SetRegistry(backup, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0, RegistryValueKind.DWord, "Widgets ocultados da barra de tarefas", changes, errors);

        if (options.ReduceTelemetry)
            SetRegistry(backup, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, RegistryValueKind.DWord, "Telemetria reduzida ao mínimo permitido", changes, errors);

        cancellationToken.ThrowIfCancellationRequested();

        var powerPlan = options.PowerPlan != PowerPlanMode.Unchanged
            ? options.PowerPlan
            : options.HighPerformancePlan ? PowerPlanMode.HighPerformance : PowerPlanMode.Unchanged;
        if (powerPlan != PowerPlanMode.Unchanged)
        {
            BackupPowerScheme(backup);
            SaveBackup(backup);
            var (scheme, label) = powerPlan switch
            {
                PowerPlanMode.PowerSaver => ("SCHEME_MAX", "Plano Economia de Energia ativado"),
                PowerPlanMode.Balanced => ("SCHEME_BALANCED", "Plano Equilibrado ativado"),
                _ => ("SCHEME_MIN", "Plano Alto Desempenho ativado")
            };
            RunPowerCfg($"/setactive {scheme}", label, changes, errors);
        }

        if (options.DisableHibernation)
        {
            backup.HibernationWasEnabled = File.Exists(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "hiberfil.sys"));
            SaveBackup(backup);
            RunPowerCfg("/hibernate off", "Hibernação desativada e espaço do hiberfil liberado", changes, errors);
        }

        if (options.DisableSysMain)
            DisableService(backup, "SysMain", "SysMain desativado", changes, errors);

        if (options.DisableSearchIndexing)
            DisableService(backup, "WSearch", "Indexação do Windows Search desativada", changes, errors);

        SaveBackup(backup);
        AppLogger.Info($"Perfil de desempenho aplicado: {changes.Count} alterações, {errors.Count} falhas.");
        return new OptimizationResult(changes, errors);
    }

    private static OptimizationResult Restore(CancellationToken cancellationToken)
    {
        var changes = new List<string>();
        var errors = new List<string>();
        if (!File.Exists(AppPaths.SettingsBackupFile))
            return new OptimizationResult(changes, new[] { "Nenhum backup de configurações foi encontrado." });

        var backup = JsonSerializer.Deserialize<OptimizationBackup>(File.ReadAllText(AppPaths.SettingsBackupFile), JsonOptions);
        if (backup is null)
            return new OptimizationResult(changes, new[] { "O backup de configurações está inválido." });

        foreach (var entry in backup.RegistryEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var hive = entry.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var key = baseKey.CreateSubKey(entry.KeyPath, true);
                if (key is null)
                    throw new InvalidOperationException("Chave indisponível.");

                if (!entry.Existed)
                    key.DeleteValue(entry.ValueName, false);
                else
                    key.SetValue(entry.ValueName, DeserializeRegistryValue(entry), ParseKind(entry.Kind));
                changes.Add($"Restaurado: {entry.ValueName}");
            }
            catch (Exception ex)
            {
                errors.Add($"{entry.ValueName}: {ex.Message}");
            }
        }

        foreach (var service in backup.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startName = service.StartType switch
            {
                2 => "auto",
                3 => "demand",
                4 => "disabled",
                _ => "demand"
            };
            RunSc($"config {service.ServiceName} start= {startName}", errors);
            if (service.StartType is 2 or 3)
                RunSc($"start {service.ServiceName}", errors, ignoreFailure: true);
            changes.Add("Serviço restaurado: " + service.ServiceName);
        }

        if (!string.IsNullOrWhiteSpace(backup.PreviousPowerScheme))
            RunPowerCfg($"/setactive {backup.PreviousPowerScheme}", "Plano de energia anterior restaurado", changes, errors);

        if (backup.HibernationWasEnabled)
            RunPowerCfg("/hibernate on", "Hibernação restaurada", changes, errors);

        try
        {
            File.Delete(AppPaths.SettingsBackupFile);
        }
        catch
        {
            // Mantém o backup se não for possível removê-lo.
        }

        AppLogger.Info($"Configurações restauradas: {changes.Count} alterações, {errors.Count} falhas.");
        return new OptimizationResult(changes, errors);
    }

    private static OptimizationBackup LoadOrCreateBackup()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsBackupFile))
                return JsonSerializer.Deserialize<OptimizationBackup>(File.ReadAllText(AppPaths.SettingsBackupFile), JsonOptions)
                       ?? new OptimizationBackup();
        }
        catch
        {
            // Cria um backup novo.
        }
        return new OptimizationBackup();
    }

    private static void SetRegistry(
        OptimizationBackup backup,
        RegistryHive hive,
        string keyPath,
        string valueName,
        object value,
        RegistryValueKind kind,
        string successText,
        ICollection<string> changes,
        ICollection<string> errors)
    {
        try
        {
            BackupRegistryValue(backup, hive, keyPath, valueName);
            SaveBackup(backup);
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(keyPath, true);
            key?.SetValue(valueName, value, kind);
            changes.Add(successText);
        }
        catch (Exception ex)
        {
            errors.Add($"{valueName}: {ex.Message}");
        }
    }

    private static void BackupRegistryValue(OptimizationBackup backup, RegistryHive hive, string keyPath, string valueName)
    {
        var hiveText = hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
        if (backup.RegistryEntries.Any(item => item.Hive == hiveText && item.KeyPath == keyPath && item.ValueName == valueName))
            return;

        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(keyPath);
        var value = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var existed = value is not null;
        var kind = existed ? key!.GetValueKind(valueName) : RegistryValueKind.None;

        backup.RegistryEntries.Add(new RegistryBackupEntry
        {
            Hive = hiveText,
            KeyPath = keyPath,
            ValueName = valueName,
            Existed = existed,
            Kind = kind.ToString(),
            ValueData = SerializeRegistryValue(value, kind)
        });
    }

    private static string SerializeRegistryValue(object? value, RegistryValueKind kind)
    {
        if (value is null)
            return string.Empty;
        return kind switch
        {
            RegistryValueKind.MultiString => JsonSerializer.Serialize((string[])value),
            RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static object DeserializeRegistryValue(RegistryBackupEntry entry)
    {
        var kind = ParseKind(entry.Kind);
        return kind switch
        {
            RegistryValueKind.DWord => int.TryParse(entry.ValueData, out var intValue) ? intValue : 0,
            RegistryValueKind.QWord => long.TryParse(entry.ValueData, out var longValue) ? longValue : 0L,
            RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(entry.ValueData) ?? Array.Empty<string>(),
            RegistryValueKind.Binary => Convert.FromBase64String(entry.ValueData),
            _ => entry.ValueData
        };
    }

    private static RegistryValueKind ParseKind(string value) =>
        Enum.TryParse<RegistryValueKind>(value, out var kind) ? kind : RegistryValueKind.String;

    private static void BackupPowerScheme(OptimizationBackup backup)
    {
        if (!string.IsNullOrWhiteSpace(backup.PreviousPowerScheme))
            return;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/getactivescheme",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
            var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
            process?.WaitForExit(10_000);
            backup.PreviousPowerScheme = Regex.Match(output, @"[0-9a-fA-F\-]{36}").Value;
        }
        catch
        {
            // Backup opcional.
        }
    }

    private static void DisableService(
        OptimizationBackup backup,
        string serviceName,
        string successText,
        ICollection<string> changes,
        ICollection<string> errors)
    {
        try
        {
            if (backup.Services.All(item => !item.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)))
            {
                backup.Services.Add(new ServiceBackupEntry { ServiceName = serviceName, StartType = ReadServiceStartType(serviceName) });
                SaveBackup(backup);
            }

            RunSc($"stop {serviceName}", errors, ignoreFailure: true);
            RunSc($"config {serviceName} start= disabled", errors);
            changes.Add(successText);
        }
        catch (Exception ex)
        {
            errors.Add($"{serviceName}: {ex.Message}");
        }
    }

    private static int ReadServiceStartType(string serviceName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"qc {serviceName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
            var output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
            process?.WaitForExit(10_000);
            var match = Regex.Match(output, @"START_TYPE\s*:\s*(\d+)");
            return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 3;
        }
        catch
        {
            return 3;
        }
    }

    private static void RunPowerCfg(string arguments, string successText, ICollection<string> changes, ICollection<string> errors)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(20_000);
            if (process is { ExitCode: 0 })
                changes.Add(successText);
            else
                errors.Add("powercfg " + arguments);
        }
        catch (Exception ex)
        {
            errors.Add("powercfg: " + ex.Message);
        }
    }

    private static void RunSc(string arguments, ICollection<string> errors, bool ignoreFailure = false)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(15_000);
            if (!ignoreFailure && process is not { ExitCode: 0 })
                errors.Add("sc " + arguments);
        }
        catch (Exception ex)
        {
            if (!ignoreFailure)
                errors.Add("sc: " + ex.Message);
        }
    }

    private static void SaveBackup(OptimizationBackup backup)
    {
        AppPaths.EnsureCreated();
        File.WriteAllText(AppPaths.SettingsBackupFile, JsonSerializer.Serialize(backup, JsonOptions));
    }
}
