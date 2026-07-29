using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class CleanupPage : UserControl, IPageLifecycle
{
    private readonly DataGridView _grid = new();
    private readonly ProgressBar _progress = new() { Height = 8, Dock = DockStyle.Bottom };
    private readonly Label _status = Theme.MutedLabel("Escolha o nível e clique em analisar.");
    private readonly Button _cleanButton = Theme.Button("Limpar selecionados", true, 180);
    private readonly Button _lightButton = Theme.Button("Analisar limpeza leve", false, 180);
    private readonly Button _deepButton = Theme.Button("Analisar limpeza profunda", false, 205);
    private readonly Button _componentButton = Theme.Button("Limpar componentes do Windows", false, 240);
    private List<CleanupTarget> _targets = new();
    private CancellationTokenSource? _operationCts;

    public CleanupPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
        WireEvents();
    }

    public void OnPageShown()
    {
        if (_targets.Count == 0)
            LoadTargets(CleanupLevel.Light);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Limpeza inteligente",
            "A limpeza leve é indicada para uso frequente. A profunda inclui caches de atualização e diagnóstico."), 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 5, 0, 0)
        };
        toolbar.Controls.Add(_lightButton);
        toolbar.Controls.Add(_deepButton);
        toolbar.Controls.Add(_cleanButton);
        toolbar.Controls.Add(_componentButton);
        root.Controls.Add(toolbar, 0, 1);

        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;
        Theme.ConfigureGrid(_grid);
        _grid.ReadOnly = false;
        _grid.MultiSelect = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Selected",
            HeaderText = "",
            Width = 42,
            ReadOnly = false
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name",
            HeaderText = "Categoria",
            Width = 210,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Description",
            HeaderText = "O que será removido",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Size",
            HeaderText = "Estimativa",
            Width = 105,
            ReadOnly = true
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Files",
            HeaderText = "Arquivos",
            Width = 80,
            ReadOnly = true
        });
        card.Controls.Add(_grid);
        root.Controls.Add(card, 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 4, 14, 4) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        statusPanel.Controls.Add(_progress);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private void WireEvents()
    {
        _lightButton.Click += async (_, _) => await AnalyzeAsync(CleanupLevel.Light);
        _deepButton.Click += async (_, _) => await AnalyzeAsync(CleanupLevel.Deep);
        _cleanButton.Click += async (_, _) => await CleanSelectedAsync();
        _componentButton.Click += async (_, _) => await RunComponentCleanupAsync();
    }

    private void LoadTargets(CleanupLevel level)
    {
        _targets = CleanupCatalog.Create(level);
        _grid.Rows.Clear();
        foreach (var target in _targets)
        {
            var row = _grid.Rows[_grid.Rows.Add(
                target.Recommended,
                target.Name,
                target.Description,
                "Não analisado",
                "—")];
            row.Tag = target;
            if (!target.Recommended)
                row.DefaultCellStyle.ForeColor = Theme.Warning;
        }
        _status.Text = level == CleanupLevel.Light
            ? "Perfil leve carregado. Clique em analisar para estimar o espaço."
            : "Perfil profundo carregado. Itens amarelos exigem atenção.";
    }

    private async Task AnalyzeAsync(CleanupLevel level)
    {
        CancelPreviousOperation();
        LoadTargets(level);
        SetBusy(true);
        _operationCts = new CancellationTokenSource();
        _progress.Minimum = 0;
        _progress.Maximum = Math.Max(1, _targets.Count);
        _progress.Value = 0;

        var progress = new Progress<CleanupProgress>(item =>
        {
            _status.Text = "Analisando: " + item.CurrentItem;
            _progress.Value = Math.Clamp(item.Completed, 0, _progress.Maximum);
        });

        try
        {
            await CleanupService.AnalyzeAsync(_targets, progress, _operationCts.Token);
            RefreshRows();
            var total = _targets.Sum(target => target.EstimatedBytes);
            _status.Text = $"Análise concluída: {CleanupResult.FormatBytes(total)} encontrados em {_targets.Sum(target => target.EstimatedFiles):N0} arquivos.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Análise cancelada.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha na análise: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshRows()
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not CleanupTarget target)
                continue;
            row.Cells["Size"].Value = target.ActionKind == CleanupActionKind.RecycleBin
                ? "Ao limpar"
                : CleanupResult.FormatBytes(target.EstimatedBytes);
            row.Cells["Files"].Value = target.ActionKind == CleanupActionKind.RecycleBin
                ? "—"
                : target.EstimatedFiles.ToString("N0");
        }
    }

    private async Task CleanSelectedAsync()
    {
        _grid.EndEdit();
        var selected = _grid.Rows.Cast<DataGridViewRow>()
            .Where(row => Convert.ToBoolean(row.Cells["Selected"].Value ?? false))
            .Select(row => row.Tag as CleanupTarget)
            .Where(target => target is not null)
            .Cast<CleanupTarget>()
            .ToArray();

        if (selected.Length == 0)
        {
            MessageBox.Show("Marque pelo menos uma categoria.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var hasDeepOrSensitive = selected.Any(target => target.MinimumLevel == CleanupLevel.Deep || !target.Recommended);
        var warning = hasDeepOrSensitive
            ? "Há itens profundos ou opcionais selecionados. Feche navegadores e salve seu trabalho. Continuar?"
            : "Executar a limpeza dos itens selecionados? Arquivos em uso serão preservados.";

        if (MessageBox.Show(warning, "Confirmar limpeza", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        CancelPreviousOperation();
        SetBusy(true);
        _operationCts = new CancellationTokenSource();
        _progress.Maximum = Math.Max(1, selected.Length);
        _progress.Value = 0;
        var progress = new Progress<CleanupProgress>(item =>
        {
            _status.Text = "Limpando: " + item.CurrentItem;
            _progress.Value = Math.Clamp(item.Completed, 0, _progress.Maximum);
        });

        try
        {
            var result = await CleanupService.CleanAsync(selected, progress, _operationCts.Token);
            _status.Text = $"Concluído: {result.FreedText}, {result.FilesDeleted:N0} arquivos, {result.Errors:N0} itens em uso/sem acesso.";
            MessageBox.Show(
                $"Espaço removido: {result.FreedText}\nArquivos removidos: {result.FilesDeleted:N0}\nItens ignorados: {result.Errors:N0}",
                "Limpeza concluída",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            await AnalyzeAsync(_targets.Any(target => target.MinimumLevel == CleanupLevel.Deep) ? CleanupLevel.Deep : CleanupLevel.Light);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Limpeza cancelada.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunComponentCleanupAsync()
    {
        if (MessageBox.Show(
                "O DISM removerá componentes antigos substituídos por atualizações. Pode demorar e não deve ser interrompido. Continuar?",
                "Limpeza de componentes",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true);
        _status.Text = "Executando DISM /StartComponentCleanup...";
        try
        {
            var result = await SystemRepairService.RunComponentCleanupAsync();
            _status.Text = result.Success ? "Limpeza de componentes concluída." : "DISM terminou com falha.";
            MessageBox.Show(result.CombinedOutput.Trim(), "Resultado do DISM", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _lightButton.Enabled = !busy;
        _deepButton.Enabled = !busy;
        _cleanButton.Enabled = !busy;
        _componentButton.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void CancelPreviousOperation()
    {
        _operationCts?.Cancel();
        _operationCts?.Dispose();
        _operationCts = null;
    }
}
