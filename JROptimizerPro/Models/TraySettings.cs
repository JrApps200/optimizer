namespace JROptimizerPro.Models;

internal enum TrayMetric
{
    Temperature,
    Cpu,
    Memory,
    Processes,
    AppIcon
}

internal sealed class TraySettings
{
    public TrayMetric Metric { get; set; } = TrayMetric.Temperature;
    public int RefreshSeconds { get; set; } = 2;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool ShowNotifications { get; set; } = true;
}
