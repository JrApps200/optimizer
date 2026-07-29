using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class DefaultsPage : UserControl, IPageLifecycle
{
    private readonly DataGridView _grid = new();
    private readonly Label _status = Theme.MutedLabel("O Windows 11 exige confirmação do usuário para trocar associações padrão.");

    public DefaultsPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        Padding = new Padding(26, 22, 26, 20);
        BuildInterface();
    }

    public void OnPageShown() => RefreshAssociations();

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        Controls.Add(root);

        root.Controls.Add(Theme.Header(
            "Aplicativos padrão",
            "Veja os manipuladores atuais e abra diretamente a tela oficial do Windows para navegador, PDF, fotos, vídeos e e-mail."), 0, 0);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        var open = Theme.Button("Abrir Apps padrão do Windows", true, 225);
        open.Click += (_, _) => DefaultAppsService.OpenDefaultAppsSettings();
        var refresh = Theme.Button("Atualizar", false, 110);
        refresh.Click += (_, _) => RefreshAssociations();
        var installed = Theme.Button("Abrir aplicativos instalados", false, 205);
        installed.Click += (_, _) => DefaultAppsService.OpenAppsFeaturesSettings();
        toolbar.Controls.Add(open);
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(installed);
        root.Controls.Add(toolbar, 0, 1);

        var card = Theme.CardPanel(12);
        card.Dock = DockStyle.Fill;
        Theme.ConfigureGrid(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Category",
            HeaderText = "Categoria",
            Width = 230
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Handler",
            HeaderText = "Manipulador atual (ProgId)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        card.Controls.Add(_grid);
        root.Controls.Add(card, 0, 2);

        var info = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card, Padding = new Padding(14, 0, 14, 0) };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        info.Controls.Add(_status);
        root.Controls.Add(info, 0, 3);
    }

    private void RefreshAssociations()
    {
        try
        {
            _grid.Rows.Clear();
            foreach (var association in DefaultAppsService.ReadCommonAssociations())
                _grid.Rows.Add(association.Category, association.CurrentHandler);
            _status.Text = "Lista atualizada. Clique em “Abrir Apps padrão do Windows” para alterar com segurança.";
        }
        catch (Exception ex)
        {
            _status.Text = "Falha ao ler associações: " + ex.Message;
        }
    }
}
