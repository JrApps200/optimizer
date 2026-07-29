using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class TrayMonitorPage : UserControl
{
    private readonly TrayMonitorService _service;
    private readonly ComboBox _metric = new();
    private readonly NumericUpDown _refresh = new();
    private readonly CheckBox _minimize = new();
    private readonly CheckBox _close = new();
    private readonly CheckBox _startMinimized = new();
    private readonly CheckBox _notifications = new();
    private readonly Label _status = Theme.MutedLabel(string.Empty);

    public TrayMonitorPage(TrayMonitorService service)
    {
        _service = service;
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Padding = new Padding(30, 25, 30, 25);
        Build();
        LoadSettings();
    }

    private void Build()
    {
        var root = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Theme.Window
        };
        Controls.Add(root);
        root.Controls.Add(Theme.Header(
            "Monitor da bandeja",
            "Escolha o dado exibido no quadradinho ao lado do relógio. As preferências permanecem salvas."));

        var card = Theme.CardPanel(22);
        card.Width = 820;
        card.Height = 390;
        root.Controls.Add(card);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(4),
            BackColor = Theme.Card
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        card.Controls.Add(layout);

        _metric.DropDownStyle = ComboBoxStyle.DropDownList;
        _metric.Width = 260;
        _metric.Items.AddRange(new object[]
        {
            "Temperatura da CPU", "Uso da CPU", "Uso da RAM",
            "Número de processos", "Apenas ícone JR"
        });

        _refresh.Minimum = 1;
        _refresh.Maximum = 10;
        _refresh.Width = 90;
        _refresh.BackColor = Theme.Window;
        _refresh.ForeColor = Theme.Text;

        ConfigureCheck(_minimize, "Minimizar para a bandeja");
        ConfigureCheck(_close, "Continuar funcionando ao fechar a janela");
        ConfigureCheck(_startMinimized, "Iniciar minimizado somente com o Windows");
        ConfigureCheck(_notifications, "Mostrar avisos do monitor");

        AddRow(layout, 0, "Mostrar em tempo real", _metric);
        AddRow(layout, 1, "Atualizar a cada (segundos)", _refresh);
        layout.Controls.Add(_minimize, 1, 2);
        layout.Controls.Add(_close, 1, 3);
        layout.Controls.Add(_startMinimized, 1, 4);
        layout.Controls.Add(_notifications, 1, 5);

        var save = Theme.Button("Salvar e aplicar", true, 190);
        save.Click += (_, _) => Save();
        var test = Theme.Button("Testar ícone agora", false, 180);
        test.Click += (_, _) =>
        {
            _service.RecreateIcon();
            _service.ShowMessage("Monitor ativo", "O ícone foi recriado. Procure-o ao lado do relógio.");
            _status.ForeColor = Theme.Success;
            _status.Text = "Ícone recriado.";
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(save);
        buttons.Controls.Add(test);
        buttons.Controls.Add(_status);
        layout.Controls.Add(buttons, 1, 6);

        var note = Theme.MutedLabel(
            "Dica: se a temperatura aparecer como N/D, o notebook não expõe o sensor ao Windows. " +
            "Nesse caso, escolha CPU, RAM ou processos. Clique duas vezes no ícone para reabrir o aplicativo.");
        note.MaximumSize = new Size(650, 0);
        layout.Controls.Add(note, 1, 7);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.Controls.Add(Theme.MutedLabel(label), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void ConfigureCheck(CheckBox checkBox, string text)
    {
        checkBox.Text = text;
        checkBox.AutoSize = true;
        checkBox.ForeColor = Theme.Text;
        checkBox.Font = new Font("Segoe UI", 9.5F);
    }

    private void LoadSettings()
    {
        var settings = _service.Settings;
        _metric.SelectedIndex = settings.Metric switch
        {
            TrayMetric.Temperature => 0,
            TrayMetric.Cpu => 1,
            TrayMetric.Memory => 2,
            TrayMetric.Processes => 3,
            _ => 4
        };
        _refresh.Value = Math.Clamp(settings.RefreshSeconds, 1, 10);
        _minimize.Checked = settings.MinimizeToTray;
        _close.Checked = settings.CloseToTray;
        _startMinimized.Checked = settings.StartMinimized;
        _notifications.Checked = settings.ShowNotifications;
    }

    private void Save()
    {
        var settings = new TraySettings
        {
            Metric = _metric.SelectedIndex switch
            {
                0 => TrayMetric.Temperature,
                1 => TrayMetric.Cpu,
                2 => TrayMetric.Memory,
                3 => TrayMetric.Processes,
                _ => TrayMetric.AppIcon
            },
            RefreshSeconds = (int)_refresh.Value,
            MinimizeToTray = _minimize.Checked,
            CloseToTray = _close.Checked,
            StartMinimized = _startMinimized.Checked,
            ShowNotifications = _notifications.Checked
        };

        try
        {
            _service.Apply(settings);
            _status.ForeColor = Theme.Success;
            _status.Text = "Configuração salva.";
        }
        catch (Exception ex)
        {
            _status.ForeColor = Theme.Danger;
            _status.Text = "Não foi possível salvar: " + ex.Message;
        }
    }
}
