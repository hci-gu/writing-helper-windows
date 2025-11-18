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
        Text = "Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16, 18, 16, 16);

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
            Text = "Provide your OpenAI API key to enable the assistant. If the OPENAI_API_KEY environment variable is set, it will be used instead.",
        };

        var apiKeyLabel = new Label
        {
            AutoSize = true,
            Text = "OpenAI API Key",
            Margin = new Padding(0, 12, 0, 2),
        };

        _apiKeyTextBox = new TextBox
        {
            Width = 360,
            UseSystemPasswordChar = true,
        };

        var promptPreambleLabel = new Label
        {
            AutoSize = true,
            Text = "About you (optional)",
            Margin = new Padding(0, 12, 0, 2),
        };

        _promptPreambleTextBox = new TextBox
        {
            Width = 360,
            Height = 120,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
        };

        var promptInstructions = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            ForeColor = Color.FromArgb(90, 90, 90),
            Text = "Anything you write here will be added to the beginning of prompts so the assistant knows more about you.",
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(180, 0, 0),
            Margin = new Padding(0, 6, 0, 0),
        };

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 14, 0, 0),
        };

        var saveButton = new Button
        {
            Text = "Save",
            AutoSize = true,
            DialogResult = DialogResult.OK,
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };

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

        _statusLabel.Text = "An API key is required.";
    }
}
