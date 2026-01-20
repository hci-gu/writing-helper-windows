using System;
using System.Drawing;
using System.Windows.Forms;
using GlobalTextHelper.Infrastructure.OpenAi;

namespace GlobalTextHelper.UI;

public sealed class SettingsForm : Form
{
    private readonly TextBox _promptPreambleTextBox;
    private readonly CheckBox _popupOnCopyCheckBox;
    private readonly NumericUpDown _minimumPopupLengthUpDown;
    private readonly Label _snoozeStatusLabel;
    private readonly Button _resetSnoozeButton;
    private readonly ComboBox _modelComboBox;
    private DateTime? _snoozeUntilUtc;

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
            RowCount = 12,
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

        var modelLabel = new Label
        {
            AutoSize = true,
            Text = "AI-modell",
            Margin = new Padding(0, Theme.PaddingMedium, 0, 4),
            Font = Theme.ButtonFont,
            ForeColor = Theme.TextColor
        };

        _modelComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 360,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            BackColor = Theme.SurfaceColor
        };

        _modelComboBox.Items.AddRange(new object[]
        {
            new ModelOption("GPT-5 Mini (standard)", OpenAiChatClient.DefaultModel),
            new ModelOption("GPT-4o Mini", OpenAiChatClient.AlternateModel)
        });
        _modelComboBox.SelectedIndex = 0;

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

        var snoozeLabel = new Label
        {
            AutoSize = true,
            Text = "Pausad popup",
            Margin = new Padding(0, Theme.PaddingMedium, 0, 4),
            Font = Theme.ButtonFont,
            ForeColor = Theme.TextColor
        };

        _snoozeStatusLabel = new Label
        {
            AutoSize = true,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextMutedColor,
            Margin = new Padding(0, 6, 12, 0)
        };

        _resetSnoozeButton = ActionButtonFactory.CreateSecondaryActionButton("Återställ snooze");
        _resetSnoozeButton.Margin = new Padding(0);
        _resetSnoozeButton.Click += (_, __) =>
        {
            _snoozeUntilUtc = null;
            UpdateSnoozeStatusLabel();
        };

        var snoozePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        snoozePanel.Controls.Add(_snoozeStatusLabel);
        snoozePanel.Controls.Add(_resetSnoozeButton);

        var minimumPopupLengthLabel = new Label
        {
            AutoSize = true,
            Text = "Minsta textlängd för popup (tecken, standard 50)",
            Margin = new Padding(0, Theme.PaddingMedium, 0, 4),
            Font = Theme.ButtonFont,
            ForeColor = Theme.TextColor
        };

        _minimumPopupLengthUpDown = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 10000,
            Width = 120,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            BackColor = Theme.SurfaceColor
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
        layout.Controls.Add(modelLabel, 0, 1);
        layout.Controls.Add(_modelComboBox, 0, 2);
        layout.Controls.Add(_popupOnCopyCheckBox, 0, 3);
        layout.Controls.Add(snoozeLabel, 0, 4);
        layout.Controls.Add(snoozePanel, 0, 5);
        layout.Controls.Add(minimumPopupLengthLabel, 0, 6);
        layout.Controls.Add(_minimumPopupLengthUpDown, 0, 7);
        layout.Controls.Add(promptPreambleLabel, 0, 8);
        layout.Controls.Add(_promptPreambleTextBox, 0, 9);
        layout.Controls.Add(promptInstructions, 0, 10);
        layout.Controls.Add(buttonPanel, 0, 11);

        Controls.Add(layout);
        UpdateSnoozeStatusLabel();
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

    public int MinimumPopupTextLength
    {
        get => (int)_minimumPopupLengthUpDown.Value;
        set => _minimumPopupLengthUpDown.Value = Math.Clamp(value, (int)_minimumPopupLengthUpDown.Minimum, (int)_minimumPopupLengthUpDown.Maximum);
    }

    public DateTime? SnoozeUntilUtc
    {
        get => _snoozeUntilUtc;
        set
        {
            _snoozeUntilUtc = value.HasValue && value.Value <= DateTime.UtcNow ? null : value;
            UpdateSnoozeStatusLabel();
        }
    }

    public string? SelectedModel
    {
        get => (_modelComboBox.SelectedItem as ModelOption)?.Value;
        set
        {
            string desired = string.IsNullOrWhiteSpace(value) ? OpenAiChatClient.DefaultModel : value;
            for (int i = 0; i < _modelComboBox.Items.Count; i++)
            {
                if (_modelComboBox.Items[i] is ModelOption option &&
                    string.Equals(option.Value, desired, StringComparison.OrdinalIgnoreCase))
                {
                    _modelComboBox.SelectedIndex = i;
                    return;
                }
            }

            _modelComboBox.SelectedIndex = 0;
        }
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateSnoozeStatusLabel()
    {
        if (_snoozeUntilUtc.HasValue)
        {
            var localTime = _snoozeUntilUtc.Value.ToLocalTime();
            _snoozeStatusLabel.Text = $"Snoozad till {localTime:yyyy-MM-dd HH:mm}";
            _resetSnoozeButton.Enabled = true;
        }
        else
        {
            _snoozeStatusLabel.Text = "Ingen snooze aktiv.";
            _resetSnoozeButton.Enabled = false;
        }
    }

    private sealed record ModelOption(string Label, string Value)
    {
        public override string ToString() => Label;
    }
}
