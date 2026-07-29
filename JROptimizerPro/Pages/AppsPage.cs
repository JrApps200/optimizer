using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class AppsPage : UserControl, IPageLifecycle
{
    private readonly DataGridView _grid = new();
    private readonly TextBox _search = new();
    private readonly Label _status = Theme.MutedLabel("Clique em atualizar para carregar os aplicativos.");
    private readonly Button _refreshButton = Theme.Button("Atualizar lista", false, 140);
    private readonly Button _uninstallButton = Theme.Button("Desinstalar selecionados", true, 190);
    private readonly Button _residueButton = Theme.Button("Localizar resíduos", false, 160);
    private readonly Button _settingsButton = Theme.Button("Configurações do Windows", false, 195);
    private readonly CheckBox _scanAfter = new()
    {
        Text = "Procurar resíduos após desinstalar",
        AutoSize = true,
        Checked = true,
        ForeColor = Theme.Muted,
        Margin = new Padding(8, 11, 0, 0)
    };

    private List<InstalledApp> _allApps = new();
    private bool _loaded;

    public AppsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
        WireEvents();
    }

    public async void OnPageShown()
    {
        if (!_loaded)
            await RefreshAppsAsync();
    }

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Aplicativos e resíduos",
            "Selecione vários programas para desinstalar. Resíduos são movidos para quarentena, não apagados imediatamente."), 0, 0);

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _search.Dock = DockStyle.Fill;
        _search.PlaceholderText = "Pesquisar nome, fabricante ou versão...";
        _search.BackColor = Theme.Card;
        _search.ForeColor = Theme.Text;
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.Margin = new Padding(0, 5, 0, 7);
        toolbar.Controls.Add(_search, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        buttons.Controls.Add(_refreshButton);
        buttons.Controls.Add(_uninstallButton);
        buttons.Controls.Add(_residueButton);
        buttons.Controls.Add(_settingsButton);
        buttons.Controls.Add(_scanAfter);
        toolbar.Controls.Add(buttons, 0, 1);
        root.Controls.Add(toolbar, 0, 1);

        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;
        Theme.ConfigureGrid(_grid);
        _grid.ReadOnly = false;
        _grid.MultiSelect = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "", Width = 42, ReadOnly = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Aplicativo", Width = 250, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Version", HeaderText = "Versão", Width = 105, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Publisher", HeaderText = "Fabricante", Width = 180, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Tipo", Width = 135, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", HeaderText = "Local", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
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
        _refreshButton.Click += async (_, _) => await RefreshAppsAsync();
        _uninstallButton.Click += async (_, _) => await UninstallSelectedAsync();
        _residueButton.Click += async (_, _) => await FindResiduesAsync(GetSelectedApps());
        _settingsButton.Click += (_, _) => DefaultAppsService.OpenAppsFeaturesSettings();
        _search.TextChanged += (_, _) => ApplyFilter();
    }

    private async Task RefreshAppsAsync()
    {
        SetBusy(true, "Carregando programas clássicos e aplicativos da Microsoft Store...");
        try
        {
            _allApps = await InstalledAppsService.LoadAsync();
            _loaded = true;
            ApplyFilter();
            _status.Text = $"{_allApps.Count:N0} aplicativos encontrados.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao carregar aplicativos: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyFilter()
    {
        var query = _search.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allApps
            : _allApps.Where(app =>
                    app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || app.Publisher.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                    || app.Version.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        _grid.Rows.Clear();
        foreach (var app in filtered)
        {
            var row = _grid.Rows[_grid.Rows.Add(
                false,
                app.Name,
                app.Version,
                app.Publisher,
                app.TypeText,
                app.InstallLocation)];
            row.Tag = app;
        }
        _status.Text = $"Exibindo {filtered.Count:N0} de {_allApps.Count:N0} aplicativos.";
    }

    private List<InstalledApp> GetSelectedApps()
    {
        _grid.EndEdit();
        return _grid.Rows.Cast<DataGridViewRow>()
            .Where(row => Convert.ToBoolean(row.Cells["Selected"].Value ?? false))
            .Select(row => row.Tag as InstalledApp)
            .Where(app => app is not null)
            .Cast<InstalledApp>()
            .ToList();
    }

    private async Task UninstallSelectedAsync()
    {
        var selected = GetSelectedApps();
        if (selected.Count == 0)
        {
            MessageBox.Show("Marque pelo menos um aplicativo.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var preview = string.Join("\n", selected.Take(10).Select(app => "• " + app.Name));
        if (selected.Count > 10)
            preview += $"\n• e mais {selected.Count - 10}...";

        if (MessageBox.Show(
                $"Os aplicativos serão desinstalados um por vez. Alguns podem abrir o desinstalador próprio.\n\n{preview}\n\nContinuar?",
                "Desinstalação em massa",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true, "Iniciando desinstalações...");
        try
        {
            var progress = new Progress<string>(message => _status.Text = message);
            var results = await UninstallService.UninstallManyAsync(selected, progress);
            var successful = results.Where(result => result.Success).Select(result => result.App).ToList();
            var failed = results.Where(result => !result.Success).ToList();

            var summary = $"Concluídos: {successful.Count}\nFalhas/cancelados: {failed.Count}";
            if (failed.Count > 0)
                summary += "\n\n" + string.Join("\n", failed.Select(item => $"• {item.App.Name}: {item.Message}"));

            MessageBox.Show(summary, "Resultado da desinstalação", MessageBoxButtons.OK,
                failed.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (_scanAfter.Checked && successful.Count > 0)
                await FindResiduesAsync(successful);

            await RefreshAppsAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task FindResiduesAsync(IReadOnlyList<InstalledApp> apps)
    {
        if (apps.Count == 0)
        {
            MessageBox.Show("Marque um ou mais aplicativos para procurar resíduos.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true, "Procurando pastas residuais com correspondência conservadora...");
        try
        {
            var candidates = await ResidueService.FindCandidatesAsync(apps);
            if (candidates.Count == 0)
            {
                MessageBox.Show("Nenhum resíduo seguro foi identificado.", "Resíduos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new ResidueReviewForm(candidates);
            dialog.ShowDialog(FindForm());
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _refreshButton.Enabled = !busy;
        _uninstallButton.Enabled = !busy;
        _residueButton.Enabled = !busy;
        _settingsButton.Enabled = !busy;
        _search.Enabled = !busy;
        UseWaitCursor = busy;
        if (!string.IsNullOrWhiteSpace(message))
            _status.Text = message;
    }
}
