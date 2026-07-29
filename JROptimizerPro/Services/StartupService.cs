using System.Text.Json;
using JROptimizerPro.Core;
using JROptimizerPro.Models;
using Microsoft.Win32;

namespace JROptimizerPro.Services;

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<StartupItem> Load()
    {
        var items = new List<StartupItem>();
        ReadRegistry(RegistryHive.CurrentUser, RegistryView.Default, StartupSource.CurrentUserRegistry, items);
        ReadRegistry(RegistryHive.LocalMachine, RegistryView.Registry64, StartupSource.LocalMachineRegistry64, items);
        ReadRegistry(RegistryHive.LocalMachine, RegistryView.Registry32, StartupSource.LocalMachineRegistry32, items);

        ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            StartupSource.UserStartupFolder,
            items);
        ReadStartupFolder(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            StartupSource.CommonStartupFolder,
            items);

        foreach (var disabled in LoadDisabled())
        {
            items.Add(new StartupItem
            {
                Id = disabled.Id,
                Name = disabled.Name,
                Command = disabled.Command,
                Source = disabled.Source,
                IsEnabled = false,
                OriginalLocation = disabled.OriginalLocation,
                BackupLocation = disabled.BackupLocation
            });
        }

        return items.OrderByDescending(item => item.IsEnabled).ThenBy(item => item.Name).ToList();
    }

    public static IReadOnlyList<string> Disable(IReadOnlyList<StartupItem> items)
    {
        var messages = new List<string>();
        var disabled = LoadDisabled();
        var disabledRoot = Path.Combine(AppPaths.Backups, "StartupFiles");
        Directory.CreateDirectory(disabledRoot);

        foreach (var item in items.Where(item => item.IsEnabled))
        {
            DisabledStartupRecord? pendingRecord = null;
            try
            {
                if (IsRegistrySource(item.Source))
                {
                    pendingRecord = new DisabledStartupRecord
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Command = item.Command,
                        Source = item.Source,
                        OriginalLocation = RunKey
                    };
                    disabled.Add(pendingRecord);
                    SaveDisabled(disabled);

                    using var key = OpenRegistryKey(item.Source, writable: true);
                    key?.DeleteValue(item.Name, false);
                }
                else
                {
                    var original = item.OriginalLocation;
                    if (!File.Exists(original))
                    {
                        messages.Add("Não encontrado: " + item.Name);
                        continue;
                    }

                    var itemFolder = Path.Combine(disabledRoot, item.Id);
                    Directory.CreateDirectory(itemFolder);
                    var destination = Path.Combine(itemFolder, Path.GetFileName(original));
                    pendingRecord = new DisabledStartupRecord
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Command = item.Command,
                        Source = item.Source,
                        OriginalLocation = original,
                        BackupLocation = destination
                    };
                    disabled.Add(pendingRecord);
                    SaveDisabled(disabled);
                    File.Move(original, destination, true);
                }

                messages.Add("Desativado: " + item.Name);
                AppLogger.Info("Inicialização desativada: " + item.Name);
            }
            catch (Exception ex)
            {
                if (pendingRecord is not null)
                {
                    disabled.Remove(pendingRecord);
                    SaveDisabled(disabled);
                }
                messages.Add($"Falha em {item.Name}: {ex.Message}");
            }
        }

        SaveDisabled(disabled);
        return messages;
    }

    public static IReadOnlyList<string> Restore(IReadOnlyList<StartupItem> items)
    {
        var messages = new List<string>();
        var disabled = LoadDisabled();
        var selectedIds = items.Where(item => !item.IsEnabled).Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var record in disabled.Where(item => selectedIds.Contains(item.Id)).ToArray())
        {
            try
            {
                if (IsRegistrySource(record.Source))
                {
                    using var key = OpenRegistryKey(record.Source, writable: true);
                    key?.SetValue(record.Name, record.Command, RegistryValueKind.String);
                }
                else
                {
                    if (!File.Exists(record.BackupLocation))
                    {
                        messages.Add("Backup não encontrado: " + record.Name);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(record.OriginalLocation)!);
                    File.Move(record.BackupLocation, record.OriginalLocation, true);
                }

                disabled.Remove(record);
                SaveDisabled(disabled);
                messages.Add("Restaurado: " + record.Name);
                AppLogger.Info("Inicialização restaurada: " + record.Name);
            }
            catch (Exception ex)
            {
                messages.Add($"Falha em {record.Name}: {ex.Message}");
            }
        }

        SaveDisabled(disabled);
        return messages;
    }

    public static IReadOnlyList<string> RestoreAll()
    {
        var items = Load().Where(item => !item.IsEnabled).ToArray();
        return Restore(items);
    }

    private static void ReadRegistry(
        RegistryHive hive,
        RegistryView view,
        StartupSource source,
        ICollection<StartupItem> items)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(RunKey);
            if (key is null)
                return;

            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString() ?? string.Empty;
                items.Add(new StartupItem
                {
                    Id = $"reg:{source}:{name}",
                    Name = name,
                    Command = command,
                    Source = source,
                    IsEnabled = true,
                    OriginalLocation = RunKey
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Falha lendo inicialização {source}: {ex.Message}");
        }
    }

    private static void ReadStartupFolder(string folder, StartupSource source, ICollection<StartupItem> items)
    {
        if (!Directory.Exists(folder))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                items.Add(new StartupItem
                {
                    Id = $"file:{source}:{Path.GetFileName(file)}",
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    Source = source,
                    IsEnabled = true,
                    OriginalLocation = file
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Falha lendo pasta de inicialização: {ex.Message}");
        }
    }

    private static RegistryKey? OpenRegistryKey(StartupSource source, bool writable)
    {
        var (hive, view) = source switch
        {
            StartupSource.CurrentUserRegistry => (RegistryHive.CurrentUser, RegistryView.Default),
            StartupSource.LocalMachineRegistry64 => (RegistryHive.LocalMachine, RegistryView.Registry64),
            StartupSource.LocalMachineRegistry32 => (RegistryHive.LocalMachine, RegistryView.Registry32),
            _ => throw new InvalidOperationException("A origem não é do Registro.")
        };

        var baseKey = RegistryKey.OpenBaseKey(hive, view);
        var key = baseKey.OpenSubKey(RunKey, writable) ?? (writable ? baseKey.CreateSubKey(RunKey, true) : null);
        baseKey.Dispose();
        return key;
    }

    private static bool IsRegistrySource(StartupSource source) =>
        source is StartupSource.CurrentUserRegistry
            or StartupSource.LocalMachineRegistry64
            or StartupSource.LocalMachineRegistry32;

    private static List<DisabledStartupRecord> LoadDisabled()
    {
        try
        {
            if (!File.Exists(AppPaths.StartupBackupFile))
                return new List<DisabledStartupRecord>();
            return JsonSerializer.Deserialize<List<DisabledStartupRecord>>(
                       File.ReadAllText(AppPaths.StartupBackupFile), JsonOptions)
                   ?? new List<DisabledStartupRecord>();
        }
        catch
        {
            return new List<DisabledStartupRecord>();
        }
    }

    private static void SaveDisabled(List<DisabledStartupRecord> records)
    {
        AppPaths.EnsureCreated();
        File.WriteAllText(AppPaths.StartupBackupFile, JsonSerializer.Serialize(records, JsonOptions));
    }
}
