using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

public sealed class SettingsForm : Form
{
    private readonly TextBox _apiKeyTextBox;
    private readonly Label _statusLabel;
    private readonly TextBox _promptPreambleTextBox;

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
            RowCount = 8,
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
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var instructions = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Text = "Ange din OpenAI-API-nyckel för att aktivera assistenten. Om miljövariabeln OPENAI_API_KEY är satt används den i stället.",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            Margin = new Padding(0, 0, 0, Theme.PaddingMedium)
        };

        var apiKeyLabel = new Label
        {
            AutoSize = true,
            Text = "OpenAI-API-nyckel",
            Margin = new Padding(0, 0, 0, 4),
            Font = Theme.ButtonFont, // Semibold
            ForeColor = Theme.TextColor
        };

        _apiKeyTextBox = new TextBox
        {
            Width = 360,
            UseSystemPasswordChar = true,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            BackColor = Theme.SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle
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
            Text = "Allt du skriver här läggs till i början av uppdragen så att assistenten vet mer om dig.",
            Margin = new Padding(0, 4, 0, 0)
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Theme.ErrorColor,
            Margin = new Padding(0, 6, 0, 0),
            Font = Theme.SmallFont
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
        layout.Controls.Add(apiKeyLabel, 0, 1);
        layout.Controls.Add(_apiKeyTextBox, 0, 2);
        layout.Controls.Add(_statusLabel, 0, 3);
        layout.Controls.Add(promptPreambleLabel, 0, 4);
        layout.Controls.Add(_promptPreambleTextBox, 0, 5);
        layout.Controls.Add(promptInstructions, 0, 6);
        layout.Controls.Add(buttonPanel, 0, 7);

        Controls.Add(layout);
    }

    public string? OpenAiApiKey
    {
        get => string.IsNullOrWhiteSpace(_apiKeyTextBox.Text) ? null : _apiKeyTextBox.Text.Trim();
        set => _apiKeyTextBox.Text = value ?? string.Empty;
    }

    public string? PromptPreamble
    {
        get => string.IsNullOrWhiteSpace(_promptPreambleTextBox.Text) ? null : _promptPreambleTextBox.Text.Trim();
        set => _promptPreambleTextBox.Text = value ?? string.Empty;
    }

    public bool RequireApiKey { get; set; }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!RequireApiKey || !string.IsNullOrWhiteSpace(OpenAiApiKey))
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        _statusLabel.Text = "En API-nyckel krävs.";
    }
}
