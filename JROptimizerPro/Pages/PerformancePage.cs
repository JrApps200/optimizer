using JROptimizerPro.Models;
using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class PerformancePage : UserControl, IPageLifecycle
{
    private readonly HardwareProfile _hardware;
    private readonly IReadOnlyList<PerformanceProfileDefinition> _profiles;

    private readonly CheckBox _transparency = Option("Desativar transparência", true);
    private readonly CheckBox _animations = Option("Reduzir animações e efeitos", true);
    private readonly CheckBox _gameDvr = Option("Desativar Game DVR e captura", true);
    private readonly CheckBox _webSearch = Option("Desativar resultados da web no Iniciar", true);
    private readonly CheckBox _suggestions = Option("Desativar sugestões e apps patrocinados", true);
    private readonly CheckBox _widgets = Option("Ocultar Widgets", true);
    private readonly CheckBox _highPerformance = Option("Ativar plano Alto Desempenho", true);
    private readonly CheckBox _backgroundApps = Option("Restringir aplicativos em segundo plano", false);
    private readonly CheckBox _telemetry = Option("Reduzir telemetria ao mínimo", false);
    private readonly CheckBox _sysMain = Option("Desativar SysMain", false);
    private readonly CheckBox _searchIndex = Option("Desativar indexação do Windows Search", false);
    private readonly CheckBox _hibernation = Option("Desativar hibernação", false);
    private readonly CheckBox _autoStart = Option("Iniciar JR Optimizer com o Windows", false);

    private readonly Label _status = Theme.MutedLabel("Escolha um perfil ou personalize cada ajuste.");
    private readonly Button _applyCustom = Theme.Button("Aplicar personalizado", true, 190);
    private readonly Button _recommended = Theme.Button("Usar recomendação", false, 180);
    private readonly Button _restore = Theme.Button("Restaurar alterações", false, 180);
    private readonly Button _saveAutoStart = Theme.Button("Salvar inicialização", false, 180);

    public PerformancePage()
    {
        _hardware = HardwareProfileService.Detect();
        _profiles = PerformanceProfileCatalog.Create(_hardware);
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
        WireEvents();
    }

    public void OnPageShown() => _autoStart.Checked = OptimizationService.IsAutoStartEnabled();

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Desempenho adaptativo",
            "Perfis para qualquer PC, detecção automática e personalização reversível. Segurança, atualizações, Wi-Fi, áudio e drivers são preservados."), 0, 0);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            Padding = new Point(16, 7)
        };
        var profilesTab = new TabPage("Perfis automáticos") { BackColor = Theme.Window, Padding = new Padding(10) };
        var customTab = new TabPage("Personalizado") { BackColor = Theme.Window, Padding = new Padding(10) };
        profilesTab.Controls.Add(BuildProfilesPanel());
        customTab.Controls.Add(BuildCustomPanel());
        tabs.TabPages.Add(profilesTab);
        tabs.TabPages.Add(customTab);
        root.Controls.Add(tabs, 0, 1);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 0, 14, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        statusPanel.Controls.Add(_status);
        root.Controls.Add(statusPanel, 0, 2);
    }

    private Control BuildProfilesPanel()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Theme.Window };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var detected = Theme.CardPanel(14);
        detected.Dock = DockStyle.Fill;
        var recommendation = _profiles.First(item => item.Type == _hardware.RecommendedProfile).Name;
        detected.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5F),
            Text = $"PC detectado: {_hardware.Processor}\n" +
                   $"RAM: {_hardware.MemoryGb:0.#} GB  •  Processadores lógicos: {_hardware.LogicalProcessors}  •  " +
                   $"Bateria: {(_hardware.HasBattery ? "sim" : "não")}\nRecomendação automática: {recommendation}"
        });
        root.Controls.Add(detected, 0, 0);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = Theme.Window
        };
        foreach (var profile in _profiles)
            cards.Controls.Add(BuildProfileCard(profile));
        root.Controls.Add(cards, 0, 1);
        return root;
    }

    private Control BuildProfileCard(PerformanceProfileDefinition profile)
    {
        var recommended = profile.Type == _hardware.RecommendedProfile;
        var card = Theme.CardPanel(16);
        card.Width = 255;
        card.Height = 185;

        var apply = Theme.Button(recommended ? "Aplicar recomendado" : "Aplicar perfil", recommended, 205);
        apply.Dock = DockStyle.Bottom;
        apply.Click += async (_, _) => await ApplyProfileAsync(profile);

        var description = new Label
        {
            Dock = DockStyle.Fill,
            Text = profile.Description,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.7F),
            Padding = new Padding(0, 7, 0, 5)
        };
        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = (recommended ? "★  " : string.Empty) + profile.Name,
            ForeColor = recommended ? Color.FromArgb(90, 180, 255) : Theme.Text,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold)
        };
        card.Controls.Add(description);
        card.Controls.Add(apply);
        card.Controls.Add(title);
        return card;
    }

    private Control BuildCustomPanel()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Theme.Window };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));

        var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        options.Controls.Add(OptionsCard(
            "Ajustes seguros",
            "Efeitos, capturas, sugestões e plano de energia.",
            _transparency, _animations, _gameDvr, _webSearch, _suggestions, _widgets, _highPerformance), 0, 0);
        options.Controls.Add(OptionsCard(
            "Avançado",
            "Pode afetar pesquisa, pré-carregamento, notificações ou hibernação.",
            _backgroundApps, _telemetry, _sysMain, _searchIndex, _hibernation, _autoStart), 1, 0);
        root.Controls.Add(options, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 0, 0) };
        buttons.Controls.Add(_applyCustom);
        buttons.Controls.Add(_recommended);
        buttons.Controls.Add(_restore);
        buttons.Controls.Add(_saveAutoStart);
        root.Controls.Add(buttons, 0, 1);
        return root;
    }

    private static Control OptionsCard(string title, string subtitle, params CheckBox[] options)
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
        flow.Controls.Add(Theme.SectionTitle(title));
        flow.Controls.Add(Theme.MutedLabel(subtitle));
        flow.Controls.AddRange(options);
        card.Controls.Add(flow);
        return card;
    }

    private void WireEvents()
    {
        _applyCustom.Click += async (_, _) => await ApplyCustomAsync();
        _recommended.Click += (_, _) => LoadRecommendedIntoCustom();
        _restore.Click += async (_, _) => await RestoreAsync();
        _saveAutoStart.Click += (_, _) =>
        {
            var message = OptimizationService.SetAutoStart(_autoStart.Checked);
            _status.Text = message;
            MessageBox.Show(message, "Inicialização automática", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }

    private async Task ApplyProfileAsync(PerformanceProfileDefinition profile)
    {
        if (MessageBox.Show(
                $"Aplicar o perfil “{profile.Name}”?\n\n{profile.Description}\n\nUm backup reversível será criado.",
                "Aplicar perfil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        await ApplyOptionsAsync(profile.Options, profile.Name);
    }

    private async Task ApplyCustomAsync()
    {
        if ((_sysMain.Checked || _searchIndex.Checked || _hibernation.Checked || _backgroundApps.Checked)
            && MessageBox.Show(
                "Você marcou ajustes avançados que podem alterar pré-carregamento, pesquisa, hibernação ou atividade em segundo plano. Continuar?",
                "Confirmar personalizado",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        await ApplyOptionsAsync(ReadCustomOptions(), "Personalizado");
    }

    private async Task ApplyOptionsAsync(OptimizationOptions options, string profileName)
    {
        SetBusy(true, $"Aplicando {profileName}...");
        try
        {
            var result = await OptimizationService.ApplyAsync(options);
            _status.Text = $"{profileName}: {result.Changes.Count} alterações, {result.Errors.Count} ignoradas/falhas.";
            MessageBox.Show(
                string.Join("\n", result.Changes.Select(item => "✓ " + item))
                + (result.Errors.Count > 0 ? "\n\nNão aplicados:\n" + string.Join("\n", result.Errors.Select(item => "• " + item)) : string.Empty)
                + "\n\nReinicie o Windows para concluir.",
                profileName,
                MessageBoxButtons.OK,
                result.Errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private OptimizationOptions ReadCustomOptions() => new()
    {
        DisableTransparency = _transparency.Checked,
        ReduceAnimations = _animations.Checked,
        DisableGameDvr = _gameDvr.Checked,
        DisableWebSearch = _webSearch.Checked,
        DisableSuggestions = _suggestions.Checked,
        DisableWidgets = _widgets.Checked,
        HighPerformancePlan = false,
        PowerPlan = _highPerformance.Checked ? PowerPlanMode.HighPerformance : PowerPlanMode.Unchanged,
        DisableBackgroundApps = _backgroundApps.Checked,
        ReduceTelemetry = _telemetry.Checked,
        DisableSysMain = _sysMain.Checked,
        DisableSearchIndexing = _searchIndex.Checked,
        DisableHibernation = _hibernation.Checked
    };

    private void LoadRecommendedIntoCustom()
    {
        var profile = _profiles.First(item => item.Type == _hardware.RecommendedProfile);
        var options = profile.Options;
        _transparency.Checked = options.DisableTransparency;
        _animations.Checked = options.ReduceAnimations;
        _gameDvr.Checked = options.DisableGameDvr;
        _webSearch.Checked = options.DisableWebSearch;
        _suggestions.Checked = options.DisableSuggestions;
        _widgets.Checked = options.DisableWidgets;
        _highPerformance.Checked = options.PowerPlan == PowerPlanMode.HighPerformance;
        _backgroundApps.Checked = options.DisableBackgroundApps;
        _telemetry.Checked = options.ReduceTelemetry;
        _sysMain.Checked = options.DisableSysMain;
        _searchIndex.Checked = options.DisableSearchIndexing;
        _hibernation.Checked = options.DisableHibernation;
        _status.Text = $"Configuração “{profile.Name}” carregada na aba Personalizado.";
    }

    private async Task RestoreAsync()
    {
        if (MessageBox.Show(
                "Restaurar os ajustes para os valores anteriores ao primeiro perfil aplicado?",
                "Restaurar alterações",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        SetBusy(true, "Restaurando...");
        try
        {
            var result = await OptimizationService.RestoreAsync();
            _status.Text = $"Restauração: {result.Changes.Count} itens e {result.Errors.Count} falhas.";
            MessageBox.Show(
                string.Join("\n", result.Changes.Concat(result.Errors)),
                "Restauração",
                MessageBoxButtons.OK,
                result.Errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _applyCustom.Enabled = !busy;
        _recommended.Enabled = !busy;
        _restore.Enabled = !busy;
        _saveAutoStart.Enabled = !busy;
        UseWaitCursor = busy;
        if (!string.IsNullOrWhiteSpace(message))
            _status.Text = message;
    }

    private static CheckBox Option(string text, bool isChecked) => new()
    {
        Text = text,
        Checked = isChecked,
        AutoSize = true,
        ForeColor = Theme.Text,
        Font = new Font("Segoe UI", 9.5F),
        Margin = new Padding(0, 5, 0, 7)
    };
}
