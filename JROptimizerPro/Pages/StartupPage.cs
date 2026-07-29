using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class StartupPage : UserControl, IPageLifecycle
{
    private readonly DataGridView _grid = new();
    private readonly Label _status = Theme.MutedLabel("Carregando itens de inicialização...");
    private readonly Button _refreshButton = Theme.Button("Atualizar", false, 110);
    private readonly Button _disableButton = Theme.Button("Desativar selecionados", true, 185);
    private readonly Button _restoreButton = Theme.Button("Reativar selecionados", false, 175);

    public StartupPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
        WireEvents();
    }

    public void OnPageShown() => RefreshItems();

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Gerenciador de inicialização",
            "Desative programas que abrem com o Windows. Cada alteração é salva e pode ser restaurada."), 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        toolbar.Controls.Add(_refreshButton);
        toolbar.Controls.Add(_disableButton);
        toolbar.Controls.Add(_restoreButton);
        var openFolder = Theme.Button("Abrir pasta Inicializar", false, 170);
        openFolder.Click += (_, _) => CommandService.StartShell(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        toolbar.Controls.Add(openFolder);
        root.Controls.Add(toolbar, 0, 1);

        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;
        Theme.ConfigureGrid(_grid);
        _grid.ReadOnly = false;
        _grid.MultiSelect = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "", Width = 42, ReadOnly = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Item", Width = 210, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 100, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Origem", Width = 165, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Command", HeaderText = "Comando / arquivo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        card.Controls.Add(_grid);
        root.Controls.Add(card, 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 0, 14, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private void WireEvents()
    {
        _refreshButton.Click += (_, _) => RefreshItems();
        _disableButton.Click += (_, _) => DisableSelected();
        _restoreButton.Click += (_, _) => RestoreSelected();
    }

    private void RefreshItems()
    {
        try
        {
            var items = StartupService.Load();
            _grid.Rows.Clear();
            foreach (var item in items)
            {
                var row = _grid.Rows[_grid.Rows.Add(
                    false,
                    item.Name,
                    item.IsEnabled ? "Ativo" : "Desativado",
                    SourceText(item.Source),
                    item.Command)];
                row.Tag = item;
                row.DefaultCellStyle.ForeColor = item.IsEnabled ? Theme.Text : Theme.Muted;
                row.Cells["Status"].Style.ForeColor = item.IsEnabled ? Theme.Success : Theme.Warning;
            }
            _status.Text = $"{items.Count(item => item.IsEnabled)} ativos e {items.Count(item => !item.IsEnabled)} desativados pelo JR Optimizer.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao carregar inicialização: " + ex.Message;
        }
    }

    private List<StartupItem> GetSelected(bool enabled)
    {
        _grid.EndEdit();
        return _grid.Rows.Cast<DataGridViewRow>()
            .Where(row => Convert.ToBoolean(row.Cells["Selected"].Value ?? false))
            .Select(row => row.Tag as StartupItem)
            .Where(item => item is not null && item.IsEnabled == enabled)
            .Cast<StartupItem>()
            .ToList();
    }

    private void DisableSelected()
    {
        var selected = GetSelected(true);
        if (selected.Count == 0)
        {
            MessageBox.Show("Marque itens ativos para desativar.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                "Desative apenas programas conhecidos. Não remova componentes de áudio, vídeo, touchpad ou segurança. Continuar?",
                "Desativar inicialização",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var messages = StartupService.Disable(selected);
        MessageBox.Show(string.Join("\n", messages), "Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshItems();
    }

    private void RestoreSelected()
    {
        var selected = GetSelected(false);
        if (selected.Count == 0)
        {
            MessageBox.Show("Marque itens desativados para reativar.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var messages = StartupService.Restore(selected);
        MessageBox.Show(string.Join("\n", messages), "Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshItems();
    }

    private static string SourceText(StartupSource source) => source switch
    {
        StartupSource.CurrentUserRegistry => "Registro do usuário",
        StartupSource.LocalMachineRegistry64 => "Registro do sistema 64-bit",
        StartupSource.LocalMachineRegistry32 => "Registro do sistema 32-bit",
        StartupSource.UserStartupFolder => "Pasta Inicializar do usuário",
        StartupSource.CommonStartupFolder => "Pasta Inicializar geral",
        _ => source.ToString()
    };
}
