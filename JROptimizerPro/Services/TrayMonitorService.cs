using JROptimizerPro.Core;
using JROptimizerPro.Models;
using JROptimizerPro.UI;

namespace JROptimizerPro.Services;

internal sealed class TrayMonitorService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SystemMetrics _metrics = new();
    private readonly TemperatureMonitor _temperature = new();
    private readonly Icon _appIcon;
    private Icon? _dynamicIcon;
    private bool _paused;
    private bool _disposed;

    public TraySettings Settings { get; private set; }
    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    public TrayMonitorService(Icon appIcon)
    {
        _appIcon = (Icon)appIcon.Clone();
        Settings = TraySettingsService.Load();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir JR Optimizer", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Pausar monitoramento", null, TogglePaused);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair completamente", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "JR Optimizer Pro",
            ContextMenuStrip = menu,
            Visible = false
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        _timer.Tick += (_, _) => UpdateNow();
        Apply(Settings, save: false);
    }

    public void Apply(TraySettings settings, bool save = true)
    {
        Settings = settings;
        _timer.Interval = Math.Clamp(settings.RefreshSeconds, 1, 10) * 1000;
        if (save)
            TraySettingsService.Save(settings);
        _timer.Start();
        UpdateNow();
    }

    public void ShowMessage(string title, string message)
    {
        if (Settings.ShowNotifications)
            _notifyIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
    }

    private void TogglePaused(object? sender, EventArgs e)
    {
        _paused = !_paused;
        _timer.Enabled = !_paused;
        if (sender is ToolStripMenuItem item)
            item.Text = _paused ? "Retomar monitoramento" : "Pausar monitoramento";
        _notifyIcon.Text = _paused ? "JR Optimizer Pro — monitor pausado" : "JR Optimizer Pro";
        if (!_paused)
            UpdateNow();
    }

    private void UpdateNow()
    {
        var snapshot = _metrics.Read();
        var temperature = _temperature.ReadCpuTemperature();
        var (text, level) = GetDisplay(snapshot, temperature);

        _dynamicIcon?.Dispose();
        _dynamicIcon = Settings.Metric == TrayMetric.AppIcon
            ? null
            : TrayIconRenderer.Create(text, LevelColor(level));
        _notifyIcon.Icon = _dynamicIcon ?? _appIcon;
        if (!_notifyIcon.Visible)
            _notifyIcon.Visible = true;

        var temperatureText = temperature.HasValue ? $"{temperature.Value:0} °C" : "N/D";
        _notifyIcon.Text = LimitTooltip(
            $"JR Optimizer Pro\nCPU: {snapshot.CpuPercent:0}% | RAM: {snapshot.MemoryPercent:0}%\n" +
            $"Processos: {snapshot.ProcessCount} | Temperatura: {temperatureText}");
    }

    private (string Text, double Level) GetDisplay(MetricsSnapshot snapshot, float? temperature) =>
        Settings.Metric switch
        {
            TrayMetric.Temperature => temperature.HasValue
                ? ($"{temperature.Value:0}", Math.Clamp((temperature.Value - 35) / 60d * 100, 0, 100))
                : ("N/D", 0),
            TrayMetric.Cpu => ($"{snapshot.CpuPercent:0}", snapshot.CpuPercent),
            TrayMetric.Memory => ($"{snapshot.MemoryPercent:0}", snapshot.MemoryPercent),
            TrayMetric.Processes => (snapshot.ProcessCount.ToString(), Math.Clamp(snapshot.ProcessCount / 2.5, 0, 100)),
            _ => ("JR", 0)
        };

    private static Color LevelColor(double level) =>
        level >= 85 ? Theme.Danger : level >= 70 ? Theme.Warning : Color.FromArgb(30, 170, 255);

    private static string LimitTooltip(string text) => text.Length <= 127 ? text : text[..127];

    public void RecreateIcon()
    {
        _notifyIcon.Visible = false;
        UpdateNow();
        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _dynamicIcon?.Dispose();
        _appIcon.Dispose();
        _temperature.Dispose();
    }
}
