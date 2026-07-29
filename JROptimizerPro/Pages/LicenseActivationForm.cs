using JROptimizerPro.Services;
using JROptimizerPro.UI;

namespace JROptimizerPro.Pages;

internal sealed class LicenseActivationForm : Form
{
    private readonly LicenseService _licenseService;
    private readonly TextBox _email = new();
    private readonly TextBox _purchaseCode = new();
    private readonly Label _status = new();
    private readonly Button _activate = new();
    private readonly ProgressBar _progress = new();
    private bool _initialCheckCompleted;

    public LicenseActivationForm(LicenseService licenseService)
    {
        _licenseService = licenseService;
        Text = "Ativação — JR Optimizer Pro";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(530, 430);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;

        BuildInterface();
        Shown += async (_, _) => await CheckExistingLicenseAsync();
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "ATIVE O JR OPTIMIZER PRO",
            Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(34, 30)
        };

        var subtitle = new Label
        {
            Text = "Use os mesmos dados informados na compra pela Kiwify.",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(38, 76)
        };

        var emailLabel = BuildLabel("E-mail da compra", 38, 120);
        ConfigureTextBox(_email, 38, 145);

        var codeLabel = BuildLabel("Código do pedido Kiwify", 38, 202);
        ConfigureTextBox(_purchaseCode, 38, 227);

        var hint = new Label
        {
            Text = "O código aparece no comprovante ou e-mail de confirmação da compra.",
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Location = new Point(38, 266)
        };

        _activate.Text = "ATIVAR LICENÇA";
        _activate.Location = new Point(38, 302);
        _activate.Size = new Size(454, 46);
        _activate.FlatStyle = FlatStyle.Flat;
        _activate.FlatAppearance.BorderSize = 0;
        _activate.BackColor = Theme.Accent;
        _activate.ForeColor = Color.White;
        _activate.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        _activate.Cursor = Cursors.Hand;
        _activate.Click += async (_, _) => await ActivateAsync();

        _progress.Location = new Point(38, 358);
        _progress.Size = new Size(454, 4);
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.Visible = false;

        _status.Location = new Point(38, 373);
        _status.Size = new Size(454, 40);
        _status.ForeColor = Theme.Muted;
        _status.TextAlign = ContentAlignment.TopCenter;

        Controls.AddRange([
            title,
            subtitle,
            emailLabel,
            _email,
            codeLabel,
            _purchaseCode,
            hint,
            _activate,
            _progress,
            _status
        ]);
    }

    private static Label BuildLabel(string text, int x, int y) => new()
    {
        Text = text,
        ForeColor = Theme.Text,
        Font = new Font("Segoe UI Semibold", 9F),
        AutoSize = true,
        Location = new Point(x, y)
    };

    private static void ConfigureTextBox(TextBox textBox, int x, int y)
    {
        textBox.Location = new Point(x, y);
        textBox.Size = new Size(454, 31);
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.FromArgb(32, 39, 52);
        textBox.ForeColor = Theme.Text;
    }

    private async Task CheckExistingLicenseAsync()
    {
        if (_initialCheckCompleted)
            return;

        _initialCheckCompleted = true;
        SetBusy(true, "Verificando licença...");
        var result = await _licenseService.CheckSavedLicenseAsync();
        SetBusy(false, result.Message);

        if (result.IsValid)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task ActivateAsync()
    {
        SetBusy(true, "Ativando licença...");
        var result = await _licenseService.ActivateAsync(_email.Text, _purchaseCode.Text);
        SetBusy(false, result.Message, result.IsValid);

        if (!result.IsValid)
            return;

        await Task.Delay(650);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetBusy(bool busy, string message, bool success = false)
    {
        _email.Enabled = !busy;
        _purchaseCode.Enabled = !busy;
        _activate.Enabled = !busy;
        _progress.Visible = busy;
        _status.Text = message;
        _status.ForeColor = success ? Theme.Success : Theme.Muted;
    }
}
