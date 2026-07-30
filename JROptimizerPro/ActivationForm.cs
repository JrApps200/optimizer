using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro;

internal sealed class ActivationForm : Form
{
    private readonly TextBox _key = new()
    {
        Width = 360,
        Height = 38,
        CharacterCasing = CharacterCasing.Upper,
        PlaceholderText = "VEL-XXXX-XXXX-XXXX-XXXX",
        Font = new Font("Segoe UI", 11F)
    };

    private readonly Button _activate = new()
    {
        Text = "ATIVAR LICENÇA",
        Width = 360,
        Height = 44,
        FlatStyle = FlatStyle.Flat,
        BackColor = Theme.Accent,
        ForeColor = Color.White,
        Font = new Font("Segoe UI Semibold", 10F),
        Cursor = Cursors.Hand
    };

    private readonly Label _status = new()
    {
        Width = 360,
        Height = 45,
        ForeColor = Theme.Muted,
        TextAlign = ContentAlignment.MiddleCenter
    };

    public ActivationForm()
    {
        Text = "Ativar JR Optimizer Pro";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, 330);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5F);

        var title = new Label
        {
            Text = "ATIVE O JR OPTIMIZER PRO",
            Width = 360,
            Height = 40,
            Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var help = new Label
        {
            Text = "Digite a chave recebida após a confirmação do pagamento.\nCada licença funciona em apenas um computador.",
            Width = 360,
            Height = 55,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _activate.FlatAppearance.BorderSize = 0;
        _activate.Click += async (_, _) => await ActivateAsync();
        _key.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
                await ActivateAsync();
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(40, 28, 40, 20)
        };
        layout.Controls.Add(title);
        layout.Controls.Add(help);
        layout.Controls.Add(_key);
        layout.Controls.Add(_activate);
        layout.Controls.Add(_status);
        Controls.Add(layout);
    }

    private async Task ActivateAsync()
    {
        if (_key.Text.Trim().Length < 12)
        {
            _status.Text = "Digite uma chave válida.";
            _status.ForeColor = Theme.Danger;
            return;
        }

        _activate.Enabled = false;
        _activate.Text = "VALIDANDO...";
        _status.Text = string.Empty;

        var result = await LicenseService.ActivateAsync(_key.Text);
        if (result.Valid)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        _status.Text = result.Message;
        _status.ForeColor = Theme.Danger;
        _activate.Enabled = true;
        _activate.Text = "ATIVAR LICENÇA";
    }
}
