using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class ResidueReviewForm : Form
{
    private readonly IReadOnlyList<ResidueCandidate> _candidates;
    private readonly DataGridView _grid = new();
    private readonly Label _status = Theme.MutedLabel("Nada é selecionado automaticamente para evitar remoções indevidas.");
    private readonly Button _quarantineButton = Theme.Button("Mover selecionados para quarentena", true, 250);

    public ResidueReviewForm(IReadOnlyList<ResidueCandidate> candidates)
    {
        _candidates = candidates;
        Text = "Revisar resíduos — JR Optimizer Pro";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 520);
        Size = new Size(980, 600);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5F);
        BuildInterface();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22),
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Revisar resíduos",
            "Confira cada pasta. Os itens escolhidos serão movidos para uma quarentena reversível."), 0, 0);

        var card = Theme.CardPanel(10);
        card.Dock = DockStyle.Fill;
        Theme.ConfigureGrid(_grid);
        _grid.ReadOnly = false;
        _grid.MultiSelect = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "", Width = 42, ReadOnly = false });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "App", HeaderText = "Aplicativo", Width = 180, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Path", HeaderText = "Pasta encontrada", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Tamanho", Width = 105, ReadOnly = true });
        card.Controls.Add(_grid);
        root.Controls.Add(card, 0, 1);

        foreach (var candidate in _candidates)
        {
            var row = _grid.Rows[_grid.Rows.Add(false, candidate.AppName, candidate.OriginalPath, CleanupResult.FormatBytes(candidate.EstimatedBytes))];
            row.Tag = candidate;
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 7, 0, 0) };
        var cancel = Theme.Button("Fechar", false, 110);
        cancel.Click += (_, _) => Close();
        _quarantineButton.Click += async (_, _) => await QuarantineAsync();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_quarantineButton);
        root.Controls.Add(buttons, 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(12, 0, 12, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private async Task QuarantineAsync()
    {
        _grid.EndEdit();
        var selected = _grid.Rows.Cast<DataGridViewRow>()
            .Where(row => Convert.ToBoolean(row.Cells["Selected"].Value ?? false))
            .Select(row => row.Tag as ResidueCandidate)
            .Where(item => item is not null)
            .Cast<ResidueCandidate>()
            .ToArray();

        if (selected.Length == 0)
        {
            MessageBox.Show("Marque as pastas que você reconhece como resíduos.", "JR Optimizer Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                $"Mover {selected.Length} pasta(s) para quarentena? Elas poderão ser restauradas pela aba Restaurar.",
                "Confirmar quarentena",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _quarantineButton.Enabled = false;
        UseWaitCursor = true;
        _status.Text = "Movendo para quarentena...";
        try
        {
            var manifest = await ResidueService.MoveToQuarantineAsync(selected);
            _status.Text = $"{manifest.Entries.Count} pasta(s) movidas para quarentena.";
            MessageBox.Show(_status.Text, "Quarentena", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        finally
        {
            _quarantineButton.Enabled = true;
            UseWaitCursor = false;
        }
    }
}
