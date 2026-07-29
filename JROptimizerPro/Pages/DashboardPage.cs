using JROptimizerPro.Core;
using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class DashboardPage : UserControl, IPageLifecycle
{
    private readonly SystemMetrics _metrics = new();
    private readonly ProcessMonitor _processMonitor = new();
    private readonly HardwareProfile _hardware = HardwareProfileService.Detect();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1500 };

    private readonly Label _cpuValue = MetricValue();
    private readonly Label _ramValue = MetricValue();
    private readonly Label _diskValue = MetricValue();
    private readonly Label _systemInfo = Theme.MutedLabel("Carregando...");
    private readonly Label _status = Theme.MutedLabel("Pronto.");
    private readonly ProgressBar _cpuBar = MetricBar();
    private readonly ProgressBar _ramBar = MetricBar();
    private readonly ProgressBar _diskBar = MetricBar();
    private readonly DataGridView _processGrid = new();

    public DashboardPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
        _timer.Tick += (_, _) => UpdateMetrics();
        Disposed += (_, _) => _timer.Dispose();
        _timer.Start();
    }

    public void OnPageShown() => UpdateMetrics();

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header("Dashboard", "Monitoramento leve, processos mais pesados e ações rápidas."), 0, 0);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        cards.Controls.Add(CreateMetricCard("CPU", _cpuValue, _cpuBar), 0, 0);
        cards.Controls.Add(CreateMetricCard("Memória", _ramValue, _ramBar), 1, 0);
        cards.Controls.Add(CreateMetricCard("Disco C:", _diskValue, _diskBar), 2, 0);
        root.Controls.Add(cards, 0, 1);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        content.Controls.Add(BuildProcessesCard(), 0, 0);
        content.Controls.Add(BuildQuickActionsCard(), 1, 0);
        root.Controls.Add(content, 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 0, 14, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private Control BuildProcessesCard()
    {
        var card = Theme.CardPanel();
        card.Dock = DockStyle.Fill;

        var title = Theme.SectionTitle("Processos mais pesados");
        title.Dock = DockStyle.Top;
        title.Height = 32;

        Theme.ConfigureGrid(_processGrid);
        _processGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Processo", HeaderText = "Processo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _processGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cpu", HeaderText = "CPU", Width = 80 });
        _processGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ram", HeaderText = "RAM", Width = 95 });
        _processGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID", Width = 70 });

        card.Controls.Add(_processGrid);
        card.Controls.Add(title);
        return card;
    }

    private Control BuildQuickActionsCard()
    {
        var card = Theme.CardPanel();
        card.Dock = DockStyle.Fill;

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };
        flow.Controls.Add(Theme.SectionTitle("Ações rápidas"));
        flow.Controls.Add(Theme.MutedLabel("As ações recomendadas evitam desativar Wi-Fi, áudio, drivers e segurança do Windows."));

        var cleanup = Theme.Button("Limpeza leve", true, 210);
        cleanup.Click += async (_, _) => await RunQuickCleanupAsync(cleanup);
        flow.Controls.Add(cleanup);

        var profile = Theme.Button("Aplicar perfil recomendado", true, 210);
        profile.Click += async (_, _) => await ApplyRecommendedProfileAsync(profile);
        flow.Controls.Add(profile);

        var taskManager = Theme.Button("Abrir Gerenciador de Tarefas", false, 210);
        taskManager.Click += (_, _) => CommandService.StartShell("taskmgr.exe");
        flow.Controls.Add(taskManager);

        _systemInfo.MaximumSize = new Size(290, 0);
        _systemInfo.Margin = new Padding(0, 16, 0, 0);
        flow.Controls.Add(_systemInfo);

        card.Controls.Add(flow);
        return card;
    }

    private void UpdateMetrics()
    {
        if (IsDisposed || !IsHandleCreated || !Visible)
            return;

        try
        {
            var snapshot = _metrics.Read();
            SetMetric(_cpuValue, _cpuBar, snapshot.CpuPercent, $"{snapshot.CpuPercent:N0}%");
            SetMetric(_ramValue, _ramBar, snapshot.MemoryPercent, $"{snapshot.MemoryPercent:N0}%  •  {snapshot.UsedMemoryGb:N1}/{snapshot.TotalMemoryGb:N1} GB");
            SetMetric(_diskValue, _diskBar, snapshot.DiskPercent, $"{snapshot.DiskPercent:N0}%  •  {snapshot.FreeDiskGb:N0} GB livres");

            var recommended = PerformanceProfileCatalog.Create(_hardware)
                .First(item => item.Type == _hardware.RecommendedProfile).Name;
            _systemInfo.Text = $"Processos: {snapshot.ProcessCount:N0}\n" +
                               $"Tempo ligado: {(int)snapshot.Uptime.TotalHours:00}:{snapshot.Uptime.Minutes:00}:{snapshot.Uptime.Seconds:00}\n" +
                               $"RAM instalada: {_hardware.MemoryGb:0.#} GB\nPerfil recomendado: {recommended}";

            var usages = _processMonitor.Sample(10);
            _processGrid.Rows.Clear();
            foreach (var process in usages)
                _processGrid.Rows.Add(process.Name, $"{process.CpuPercent:N1}%", $"{process.MemoryMb:N0} MB", process.ProcessId);
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao atualizar o painel: " + ex.Message;
        }
    }

    private async Task RunQuickCleanupAsync(Button button)
    {
        if (MessageBox.Show(
                "Executar a limpeza leve recomendada? Arquivos em uso serão ignorados.",
                "Limpeza leve",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        button.Enabled = false;
        _status.Text = "Executando limpeza leve...";
        try
        {
            var targets = CleanupCatalog.Create(CleanupLevel.Light)
                .Where(target => target.Recommended && target.ActionKind != CleanupActionKind.RecycleBin)
                .ToArray();
            var result = await CleanupService.CleanAsync(targets);
            _status.Text = $"Limpeza concluída: {result.FreedText}, {result.FilesDeleted:N0} arquivos.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha na limpeza: " + ex.Message;
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private async Task ApplyRecommendedProfileAsync(Button button)
    {
        if (MessageBox.Show(
                "Aplicar o perfil detectado automaticamente para este computador? Será criado um backup reversível.",
                "Perfil recomendado",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        button.Enabled = false;
        _status.Text = "Aplicando perfil recomendado...";
        try
        {
            var options = PerformanceProfileCatalog.Create(_hardware)
                .First(item => item.Type == _hardware.RecommendedProfile).Options;
            var result = await OptimizationService.ApplyAsync(options);
            _status.Text = $"Perfil aplicado: {result.Changes.Count} alterações e {result.Errors.Count} falhas.";
            MessageBox.Show(
                string.Join(Environment.NewLine, result.Changes.Select(item => "✓ " + item))
                + (result.Errors.Count > 0 ? "\n\nFalhas:\n" + string.Join("\n", result.Errors) : string.Empty)
                + "\n\nReinicie o notebook para aplicar tudo.",
                "JR Optimizer Pro",
                MessageBoxButtons.OK,
                result.Errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private static Panel CreateMetricCard(string title, Label value, ProgressBar bar)
    {
        var card = Theme.CardPanel(16);
        card.Dock = DockStyle.Fill;
        var label = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = title,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
        };
        value.Dock = DockStyle.Top;
        value.Height = 54;
        bar.Dock = DockStyle.Bottom;
        bar.Height = 10;
        card.Controls.Add(bar);
        card.Controls.Add(value);
        card.Controls.Add(label);
        return card;
    }

    private static Label MetricValue() => new()
    {
        Text = "0%",
        ForeColor = Theme.Text,
        Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static ProgressBar MetricBar() => new() { Minimum = 0, Maximum = 100, Value = 0 };

    private static void SetMetric(Label label, ProgressBar bar, double value, string text)
    {
        label.Text = text;
        bar.Value = Math.Clamp((int)Math.Round(value), 0, 100);
    }
}
