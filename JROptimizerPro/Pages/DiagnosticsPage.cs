using System.Diagnostics;
using JROptimizerPro.Core;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class DiagnosticsPage : UserControl, IPageLifecycle
{
    private readonly ProcessMonitor _monitor = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly DataGridView _grid = new();
    private readonly TextBox _output = new();
    private readonly Label _status = Theme.MutedLabel("Pronto.");
    private readonly List<Button> _actionButtons = new();
    private CancellationTokenSource? _commandCts;

    public DiagnosticsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
        _timer.Tick += (_, _) => RefreshProcesses();
        _timer.Start();
        Disposed += (_, _) => _timer.Dispose();
    }

    public void OnPageShown() => RefreshProcesses();

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
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 56F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 44F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Diagnóstico e reparo",
            "Identifique processos pesados e execute ferramentas oficiais do Windows com saída registrada."), 0, 0);

        root.Controls.Add(BuildProcessesCard(), 0, 1);
        root.Controls.Add(BuildRepairCard(), 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 0, 14, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private Control BuildProcessesCard()
    {
        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        var refresh = Theme.Button("Atualizar", false, 105);
        refresh.Click += (_, _) => RefreshProcesses();
        var end = Theme.Button("Finalizar processo", true, 145);
        end.Click += (_, _) => EndSelectedProcess();
        var taskManager = Theme.Button("Gerenciador de Tarefas", false, 180);
        taskManager.Click += (_, _) => CommandService.StartShell("taskmgr.exe");
        var resourceMonitor = Theme.Button("Monitor de Recursos", false, 165);
        resourceMonitor.Click += (_, _) => CommandService.StartShell("resmon.exe");
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(end);
        toolbar.Controls.Add(taskManager);
        toolbar.Controls.Add(resourceMonitor);

        Theme.ConfigureGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Processo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Cpu", HeaderText = "CPU", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ram", HeaderText = "RAM", Width = 100 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID", Width = 80 });

        card.Controls.Add(_grid);
        card.Controls.Add(toolbar);
        return card;
    }

    private Control BuildRepairCard()
    {
        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        AddRepairButton(toolbar, "SFC /scannow", 125, async token => await SystemRepairService.RunSfcAsync(token));
        AddRepairButton(toolbar, "DISM RestoreHealth", 155, async token => await SystemRepairService.RunDismRestoreAsync(token));
        AddRepairButton(toolbar, "CHKDSK /scan", 130, async token => await SystemRepairService.RunCheckDiskScanAsync(token));
        AddRepairButton(toolbar, "Resetar rede", 125, async token => await SystemRepairService.ResetNetworkAsync(token));

        var report = Theme.Button("Gerar relatório", false, 140);
        report.Click += (_, _) =>
        {
            var path = SystemRepairService.GenerateSystemReport();
            _status.Text = "Relatório salvo em: " + path;
            CommandService.StartShell("explorer.exe", $"/select,\"{path}\"");
        };
        toolbar.Controls.Add(report);
        _actionButtons.Add(report);

        _output.Dock = DockStyle.Fill;
        _output.Multiline = true;
        _output.ScrollBars = ScrollBars.Both;
        _output.ReadOnly = true;
        _output.WordWrap = false;
        _output.BackColor = Color.FromArgb(15, 19, 26);
        _output.ForeColor = Color.FromArgb(205, 214, 226);
        _output.BorderStyle = BorderStyle.FixedSingle;
        _output.Font = new Font("Consolas", 9F);

        card.Controls.Add(_output);
        card.Controls.Add(toolbar);
        return card;
    }

    private void AddRepairButton(Control toolbar, string text, int width, Func<CancellationToken, Task<CommandResult>> action)
    {
        var button = Theme.Button(text, false, width);
        button.Click += async (_, _) => await RunRepairAsync(text, action);
        toolbar.Controls.Add(button);
        _actionButtons.Add(button);
    }

    private void RefreshProcesses()
    {
        if (!Visible || IsDisposed)
            return;

        try
        {
            var usages = _monitor.Sample(25);
            _grid.Rows.Clear();
            foreach (var process in usages)
                _grid.Rows.Add(process.Name, $"{process.CpuPercent:N1}%", $"{process.MemoryMb:N0} MB", process.ProcessId);
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao ler processos: " + ex.Message;
        }
    }

    private void EndSelectedProcess()
    {
        if (_grid.CurrentRow is null || !int.TryParse(_grid.CurrentRow.Cells["Pid"].Value?.ToString(), out var pid))
        {
            MessageBox.Show("Selecione um processo.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var name = _grid.CurrentRow.Cells["Name"].Value?.ToString() ?? pid.ToString();
        if (pid == Environment.ProcessId)
        {
            MessageBox.Show("O próprio JR Optimizer não será finalizado por aqui.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                $"Finalizar “{name}” (PID {pid})? Dados não salvos nesse aplicativo podem ser perdidos.",
                "Finalizar processo",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(true);
            _status.Text = "Processo finalizado: " + name;
            RefreshProcesses();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Não foi possível finalizar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RunRepairAsync(string name, Func<CancellationToken, Task<CommandResult>> action)
    {
        if (MessageBox.Show(
                $"Executar {name}? A operação pode demorar. Não desligue o computador durante o processo.",
                "Ferramenta do Windows",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _commandCts?.Cancel();
        _commandCts?.Dispose();
        _commandCts = new CancellationTokenSource();
        SetBusy(true);
        _status.Text = "Executando " + name + "...";
        _output.Text = "Aguarde. A saída completa aparecerá quando o comando terminar.";

        try
        {
            var result = await action(_commandCts.Token);
            var output = result.CombinedOutput.Trim();
            if (output.Length > 50_000)
                output = output[^50_000..];
            _output.Text = output;
            _status.Text = result.Success ? name + " concluído." : name + $" retornou código {result.ExitCode}.";
        }
        catch (Exception ex)
        {
            _output.Text = ex.ToString();
            _status.Text = "Falha em " + name + ".";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        foreach (var button in _actionButtons)
            button.Enabled = !busy;
        UseWaitCursor = busy;
    }
}
