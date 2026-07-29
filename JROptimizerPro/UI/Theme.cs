using System.Reflection;

namespace JROptimizerPro.UI;

internal static class Theme
{
    public static readonly Color Window = Color.FromArgb(13, 17, 23);
    public static readonly Color Sidebar = Color.FromArgb(20, 25, 34);
    public static readonly Color Card = Color.FromArgb(24, 30, 40);
    public static readonly Color CardHover = Color.FromArgb(30, 38, 50);
    public static readonly Color Accent = Color.FromArgb(62, 110, 210);
    public static readonly Color AccentHover = Color.FromArgb(75, 125, 225);
    public static readonly Color Text = Color.FromArgb(241, 245, 249);
    public static readonly Color Muted = Color.FromArgb(155, 166, 184);
    public static readonly Color Border = Color.FromArgb(45, 54, 69);
    public static readonly Color Success = Color.FromArgb(63, 185, 122);
    public static readonly Color Warning = Color.FromArgb(245, 178, 66);
    public static readonly Color Danger = Color.FromArgb(224, 79, 95);

    public static Button Button(string text, bool primary = true, int width = 160)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Card,
            ForeColor = Text,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 8),
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.MouseEnter += (_, _) => button.BackColor = primary ? AccentHover : CardHover;
        button.MouseLeave += (_, _) => button.BackColor = primary ? Accent : Card;
        return button;
    }

    public static Label PageTitle(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold),
        ForeColor = Text,
        Margin = new Padding(0)
    };

    public static Label PageSubtitle(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI", 10F),
        ForeColor = Muted,
        MaximumSize = new Size(850, 0),
        Margin = new Padding(2, 4, 0, 18)
    };

    public static Panel CardPanel(int padding = 18) => new()
    {
        BackColor = Card,
        Padding = new Padding(padding),
        Margin = new Padding(0, 0, 14, 14)
    };

    public static Label SectionTitle(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
        ForeColor = Text,
        Margin = new Padding(0, 0, 0, 6)
    };

    public static Label MutedLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI", 9.5F),
        ForeColor = Muted,
        MaximumSize = new Size(780, 0),
        Margin = new Padding(0, 0, 0, 8)
    };

    public static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Card;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 38, 50);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 38, 50);
        grid.DefaultCellStyle.BackColor = Card;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(47, 67, 102);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 34;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = true;
        grid.ReadOnly = true;
        grid.Dock = DockStyle.Fill;

        try
        {
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null,
                grid,
                new object[] { true });
        }
        catch
        {
            // Apenas reduz flicker quando suportado.
        }
    }

    public static FlowLayoutPanel Header(string title, string subtitle)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        panel.Controls.Add(PageTitle(title));
        panel.Controls.Add(PageSubtitle(subtitle));
        return panel;
    }
}
