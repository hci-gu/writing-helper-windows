using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

public sealed class SettingsForm : Form
{
    private readonly TextBox _promptPreambleTextBox;
    private readonly CheckBox _popupOnCopyCheckBox;

    public SettingsForm()
    {
        Text = "Inställningar";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(Theme.PaddingMedium);
        BackColor = Theme.BackgroundColor;
        Font = Theme.BodyFont;

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 6,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var instructions = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Text = "Berätta kort om dig själv så att assistenten kan skriva svar som låter mer som du.",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            Margin = new Padding(0, 0, 0, Theme.PaddingMedium)
        };

        var promptPreambleLabel = new Label
        {
            AutoSize = true,
            Text = "Om dig (valfritt)",
            Margin = new Padding(0, Theme.PaddingMedium, 0, 4),
            Font = Theme.ButtonFont,
            ForeColor = Theme.TextColor
        };

        _promptPreambleTextBox = new TextBox
        {
            Width = 360,
            Height = 120,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            BackColor = Theme.SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle
        };

        var promptInstructions = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            ForeColor = Theme.TextMutedColor,
            Font = Theme.SmallFont,
            Text = "Skriv saker om dig själv här så att assistenten vet mer om dig och kan fylla ut sina svar bättre.",
            Margin = new Padding(0, 4, 0, 0)
        };

        _popupOnCopyCheckBox = new CheckBox
        {
            AutoSize = true,
            Checked = true,
            Text = "Visa popup när jag kopierar text",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            BackColor = Theme.BackgroundColor,
            Margin = new Padding(0, Theme.PaddingLarge, 0, 0)
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 14, 0, 0),
        };

        var saveButton = ActionButtonFactory.CreatePrimaryActionButton("Spara");
        saveButton.DialogResult = DialogResult.OK;

        var cancelButton = ActionButtonFactory.CreateSecondaryActionButton("Avbryt");
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(0, 0, 8, 0);

        saveButton.Click += OnSaveClicked;

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);

        layout.Controls.Add(instructions, 0, 0);
        layout.Controls.Add(_popupOnCopyCheckBox, 0, 1);
        layout.Controls.Add(promptPreambleLabel, 0, 2);
        layout.Controls.Add(_promptPreambleTextBox, 0, 3);
        layout.Controls.Add(promptInstructions, 0, 4);
        layout.Controls.Add(buttonPanel, 0, 5);

        Controls.Add(layout);
    }

    public string? PromptPreamble
    {
        get => string.IsNullOrWhiteSpace(_promptPreambleTextBox.Text) ? null : _promptPreambleTextBox.Text.Trim();
        set => _promptPreambleTextBox.Text = value ?? string.Empty;
    }

    public bool ShowPopupOnCopy
    {
        get => _popupOnCopyCheckBox.Checked;
        set => _popupOnCopyCheckBox.Checked = value;
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}
