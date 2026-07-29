using JROptimizerPro.Pages;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro;

internal sealed class MainForm : Form
{
    private readonly Panel _contentHost = new() { Dock = DockStyle.Fill, BackColor = Theme.Window };
    private readonly Dictionary<string, Control> _pages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<Control>> _pageFactories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Button> _navButtons = new(StringComparer.OrdinalIgnoreCase);
    private string _currentPage = string.Empty;
    private readonly TrayMonitorService _trayMonitor;
    private bool _allowExit;

    public MainForm()
    {
        Text = "JR Optimizer Pro 2.2";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 650);
        Size = new Size(1180, 700);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        _trayMonitor = new TrayMonitorService(Icon);
        _trayMonitor.OpenRequested += (_, _) => RestoreFromTray();
        _trayMonitor.ExitRequested += (_, _) => ExitCompletely();

        BuildInterface();
        BuildPages();
        ShowPage("Dashboard");

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized && _trayMonitor.Settings.MinimizeToTray)
                HideToTray();
        };
        FormClosing += OnFormClosing;
        Shown += (_, _) =>
        {
            if (_trayMonitor.Settings.StartMinimized &&
                Environment.GetCommandLineArgs().Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase)))
                HideToTray(showMessage: false);
        };
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(_contentHost, 1, 0);
    }

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Sidebar,
            Padding = new Padding(16, 22, 16, 18)
        };

        var logo = new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            Text = "JR OPTIMIZER",
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var version = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "PRO 2.2  •  OTIMIZAÇÃO ADAPTATIVA",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.TopLeft
        };

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 470,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = Theme.Sidebar
        };

        AddNavButton(nav, "Dashboard", "⌂  Dashboard");
        AddNavButton(nav, "Limpeza", "✦  Limpeza");
        AddNavButton(nav, "Aplicativos", "▦  Aplicativos");
        AddNavButton(nav, "Padrões", "◉  Apps padrão");
        AddNavButton(nav, "Inicialização", "↗  Inicialização");
        AddNavButton(nav, "Desempenho", "⚡  Desempenho");
        AddNavButton(nav, "Diagnóstico", "▤  Diagnóstico");
        AddNavButton(nav, "Monitor", "▣  Monitor da bandeja");
        AddNavButton(nav, "Restaurar", "↶  Restaurar e logs");

        var admin = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Text = "● Modo administrador ativo",
            ForeColor = Theme.Success,
            Font = new Font("Segoe UI Semibold", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var dataPath = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Text = "Backups e logs em\nC:\\ProgramData\\JR Optimizer Pro",
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8F),
            TextAlign = ContentAlignment.MiddleLeft
        };

        sidebar.Controls.Add(admin);
        sidebar.Controls.Add(dataPath);
        sidebar.Controls.Add(nav);
        sidebar.Controls.Add(version);
        sidebar.Controls.Add(logo);
        return sidebar;
    }

    private void AddNavButton(Control parent, string key, string text)
    {
        var button = new Button
        {
            Width = 198,
            Height = 44,
            Text = "  " + text,
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Sidebar,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 9.5F),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 5),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => ShowPage(key);
        parent.Controls.Add(button);
        _navButtons[key] = button;
    }

    private void BuildPages()
    {
        _pageFactories["Dashboard"] = () => new DashboardPage();
        _pageFactories["Limpeza"] = () => new CleanupPage();
        _pageFactories["Aplicativos"] = () => new AppsPage();
        _pageFactories["Padrões"] = () => new DefaultsPage();
        _pageFactories["Inicialização"] = () => new StartupPage();
        _pageFactories["Desempenho"] = () => new PerformancePage();
        _pageFactories["Diagnóstico"] = () => new DiagnosticsPage();
        _pageFactories["Monitor"] = () => new TrayMonitorPage(_trayMonitor);
        _pageFactories["Restaurar"] = () => new RestorePage();
    }

    private void ShowPage(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            if (!_pageFactories.TryGetValue(key, out var factory))
                return;

            page = factory();
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _contentHost.Controls.Add(page);
            _pages[key] = page;
        }

        foreach (var pair in _pages)
            pair.Value.Visible = pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase);

        foreach (var pair in _navButtons)
        {
            var active = pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase);
            pair.Value.BackColor = active ? Color.FromArgb(43, 61, 91) : Theme.Sidebar;
            pair.Value.ForeColor = active ? Theme.Text : Theme.Muted;
        }

        page.BringToFront();
        _currentPage = key;
        if (page is IPageLifecycle lifecycle)
            lifecycle.OnPageShown();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowExit || !_trayMonitor.Settings.CloseToTray)
        {
            _trayMonitor.Dispose();
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray(bool showMessage = true)
    {
        Hide();
        ShowInTaskbar = false;
        if (showMessage)
            _trayMonitor.ShowMessage("JR Optimizer Pro", "O monitor continua ativo ao lado do relógio.");
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void ExitCompletely()
    {
        _allowExit = true;
        _trayMonitor.Dispose();
        Close();
    }
}
