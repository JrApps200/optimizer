namespace JROptimizerPro.Core;

internal static class AppPaths
{
    public static string DataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "JR Optimizer Pro");

    public static string Logs { get; } = Path.Combine(DataRoot, "Logs");
    public static string Backups { get; } = Path.Combine(DataRoot, "Backups");
    public static string Quarantine { get; } = Path.Combine(DataRoot, "Quarantine");
    public static string StartupBackupFile { get; } = Path.Combine(Backups, "startup-disabled.json");
    public static string SettingsBackupFile { get; } = Path.Combine(Backups, "settings-backup.json");
    public static string TraySettingsFile { get; } = Path.Combine(DataRoot, "tray-settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(Quarantine);
    }
}
