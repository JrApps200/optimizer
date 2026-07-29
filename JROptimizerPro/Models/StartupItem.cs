namespace JROptimizerPro.Models;

internal enum StartupSource
{
    CurrentUserRegistry,
    LocalMachineRegistry64,
    LocalMachineRegistry32,
    UserStartupFolder,
    CommonStartupFolder
}

internal sealed class StartupItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public StartupSource Source { get; init; }
    public bool IsEnabled { get; init; }
    public string OriginalLocation { get; init; } = string.Empty;
    public string BackupLocation { get; init; } = string.Empty;
}

internal sealed class DisabledStartupRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public StartupSource Source { get; init; }
    public string OriginalLocation { get; init; } = string.Empty;
    public string BackupLocation { get; init; } = string.Empty;
}
