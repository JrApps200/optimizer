using JROptimizerPro.Core;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class RestorePage : UserControl, IPageLifecycle
{
    private readonly TextBox _logViewer = new();
    private readonly Label _status = Theme.MutedLabel("Backups e logs ficam em uma pasta central do sistema.");

    public RestorePage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
    }

    public void OnPageShown() => RefreshLog();

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 170F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Restaurar, quarentena e logs",
            "Desfaça otimizações, reative inicializações e restaure pastas movidas após desinstalações."), 0, 0);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        actions.Controls.Add(ActionCard("Restaurar desempenho", "Reverte os valores salvos antes das otimizações.", RestorePerformanceAsync), 0, 0);
        actions.Controls.Add(ActionCard("Reativar inicializações", "Restaura todos os itens desativados por este programa.", () => { RestoreStartup(); return Task.CompletedTask; }), 1, 0);
        actions.Controls.Add(ActionCard("Restaurar quarentena", "Move resíduos de volta para seus caminhos originais.", () => { RestoreQuarantine(); return Task.CompletedTask; }), 2, 0);
        actions.Controls.Add(ActionCard("Abrir pasta de backups", "Acesse manifests, itens desativados e quarentena.", () => { CommandService.StartShell(AppPaths.DataRoot); return Task.CompletedTask; }), 0, 1);
        actions.Controls.Add(ActionCard("Atualizar logs", "Recarrega o log diário exibido abaixo.", () => { RefreshLog(); return Task.CompletedTask; }), 1, 1);
        actions.Controls.Add(ActionCard("Esvaziar quarentena", "Apaga definitivamente os resíduos já revisados.", () => { DeleteQuarantine(); return Task.CompletedTask; }), 2, 1);
        root.Controls.Add(actions, 0, 1);

        var logCard = Theme.CardPanel(12);
        logCard.Dock = DockStyle.Fill;
        _logViewer.Dock = DockStyle.Fill;
        _logViewer.Multiline = true;
        _logViewer.ScrollBars = ScrollBars.Both;
        _logViewer.ReadOnly = true;
        _logViewer.WordWrap = false;
        _logViewer.BackColor = Color.FromArgb(15, 19, 26);
        _logViewer.ForeColor = Color.FromArgb(205, 214, 226);
        _logViewer.BorderStyle = BorderStyle.FixedSingle;
        _logViewer.Font = new Font("Consolas", 9F);
        logCard.Controls.Add(_logViewer);
        root.Controls.Add(logCard, 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 0, 14, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private Control ActionCard(string title, string description, Func<Task> action)
    {
        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;
        card.Cursor = Cursors.Hand;

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = title,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold)
        };
        var descriptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = description,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.5F)
        };

        async void Click(object? _, EventArgs __) => await action();
        card.Click += Click;
        titleLabel.Click += Click;
        descriptionLabel.Click += Click;
        card.Controls.Add(descriptionLabel);
        card.Controls.Add(titleLabel);
        return card;
    }

    private async Task RestorePerformanceAsync()
    {
        if (MessageBox.Show(
                "Restaurar todos os ajustes de desempenho para os valores anteriores?",
                "Restaurar desempenho",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _status.Text = "Restaurando configurações...";
        var result = await OptimizationService.RestoreAsync();
        _status.Text = $"Restauração concluída: {result.Changes.Count} itens e {result.Errors.Count} falhas.";
        MessageBox.Show(
            string.Join("\n", result.Changes.Concat(result.Errors)),
            "Restauração",
            MessageBoxButtons.OK,
            result.Errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        RefreshLog();
    }

    private void RestoreStartup()
    {
        var messages = StartupService.RestoreAll();
        _status.Text = messages.Count == 0 ? "Não havia itens de inicialização para restaurar." : $"{messages.Count} resultado(s) de restauração.";
        MessageBox.Show(messages.Count == 0 ? _status.Text : string.Join("\n", messages), "Inicialização", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshLog();
    }

    private void RestoreQuarantine()
    {
        if (MessageBox.Show(
                "Restaurar todas as pastas ainda existentes na quarentena?",
                "Restaurar quarentena",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        var messages = ResidueService.RestoreAll();
        _status.Text = messages.Count == 0 ? "Nenhuma pasta disponível para restauração." : $"{messages.Count} resultado(s).";
        MessageBox.Show(messages.Count == 0 ? _status.Text : string.Join("\n", messages), "Quarentena", MessageBoxButtons.OK, MessageBoxIcon.Information);
        RefreshLog();
    }

    private void DeleteQuarantine()
    {
        if (MessageBox.Show(
                "Excluir permanentemente toda a quarentena? Depois disso não será possível restaurar esses resíduos.",
                "Excluir definitivamente",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            ResidueService.DeleteQuarantinePermanently();
            _status.Text = "Quarentena esvaziada.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao esvaziar quarentena: " + ex.Message;
        }
        RefreshLog();
    }

    private void RefreshLog()
    {
        try
        {
            if (!File.Exists(AppLogger.CurrentLogFile))
            {
                _logViewer.Text = "Nenhum log criado hoje.";
                return;
            }

            var text = File.ReadAllText(AppLogger.CurrentLogFile);
            if (text.Length > 100_000)
                text = text[^100_000..];
            _logViewer.Text = text;
            _logViewer.SelectionStart = _logViewer.TextLength;
            _logViewer.ScrollToCaret();
            _status.Text = "Log atualizado: " + AppLogger.CurrentLogFile;
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao ler log: " + ex.Message;
        }
    }
}
