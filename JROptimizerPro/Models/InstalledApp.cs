namespace JROptimizerPro.Models;

internal sealed class InstalledApp
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public string InstallLocation { get; init; } = string.Empty;
    public string UninstallString { get; init; } = string.Empty;
    public string QuietUninstallString { get; init; } = string.Empty;
    public bool IsAppx { get; init; }
    public string PackageFullName { get; init; } = string.Empty;

    public string TypeText => IsAppx ? "Microsoft Store" : "Programa clássico";
}

internal sealed record UninstallResult(InstalledApp App, bool Success, string Message);
