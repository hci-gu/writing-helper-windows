using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Actions;
using GlobalTextHelper.Infrastructure.Logging;

namespace GlobalTextHelper.UI;

public sealed class EditorForm : Form
{
    private readonly IReadOnlyList<ITextAction> _actions;
    private readonly ILogger _logger;
    private readonly TextBox _inputTextBox;
    private readonly Label _messageLabel;
    private readonly FlowLayoutPanel _buttonPanel;
    private readonly FlowLayoutPanel _loadingPanel;
    private readonly Label _loadingLabel;
    private readonly TextBox _responseTextBox;
    private readonly List<ContextMenuStrip> _optionMenus = new();
    private bool _isBusy;

    public EditorForm(IEnumerable<ITextAction> actions, ILogger logger)
    {
        _actions = actions?.ToList() ?? throw new ArgumentNullException(nameof(actions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Text = "Writing Helper Editor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(520, 420);
        Padding = new Padding(16, 18, 16, 16);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 7,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            Text = "Enter or paste the text you want to edit, then choose an action below.",
            Font = new Font("Segoe UI", 9F),
        };

        _inputTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(0, 12, 0, 0)
        };

        _loadingPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Visible = false,
            Margin = new Padding(0, 12, 0, 0)
        };

        var loadingIndicator = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 25,
            Size = new Size(160, 16),
            Margin = new Padding(0, 4, 12, 0)
        };

        _loadingLabel = new Label
        {
            AutoSize = true,
            Text = "Running action…",
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.FromArgb(90, 90, 90),
            Margin = new Padding(0, 2, 0, 0)
        };

        _loadingPanel.Controls.Add(loadingIndicator);
        _loadingPanel.Controls.Add(_loadingLabel);

        _buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 12, 0, 0)
        };

        InitializeActionButtons();

        var responseLabel = new Label
        {
            AutoSize = true,
            Text = "Response",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 18, 0, 0)
        };

        _responseTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(0, 8, 0, 0),
            ReadOnly = true,
            BackColor = SystemColors.Window
        };

        var closeButtonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 18, 0, 0)
        };

        var closeButton = new Button
        {
            Text = "Close",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        closeButtonPanel.Controls.Add(closeButton);
        CancelButton = closeButton;

        layout.Controls.Add(_messageLabel, 0, 0);
        layout.Controls.Add(_inputTextBox, 0, 1);
        layout.Controls.Add(_loadingPanel, 0, 2);
        layout.Controls.Add(_buttonPanel, 0, 3);
        layout.Controls.Add(responseLabel, 0, 4);
        layout.Controls.Add(_responseTextBox, 0, 5);
        layout.Controls.Add(closeButtonPanel, 0, 6);

        Controls.Add(layout);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var menu in _optionMenus)
            {
                menu.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void InitializeActionButtons()
    {
        foreach (var menu in _optionMenus)
        {
            menu.Dispose();
        }

        _optionMenus.Clear();
        _buttonPanel.Controls.Clear();

        foreach (var action in _actions)
        {
            var button = action.IsPrimaryAction
                ? ActionButtonFactory.CreatePrimaryActionButton(action.DisplayName)
                : ActionButtonFactory.CreateSecondaryActionButton(action.DisplayName);

            if (action.Options.Count > 0)
            {
                var menu = new ContextMenuStrip();
                foreach (var option in action.Options)
                {
                    var item = new ToolStripMenuItem(option.Label) { Tag = option.Id };
                    item.Click += async (s, e) => await HandleActionInvokedAsync(action.Id, option.Id);
                    menu.Items.Add(item);
                }

                button.Click += (s, e) => menu.Show(button, new Point(0, button.Height));
                _optionMenus.Add(menu);
            }
            else
            {
                button.Click += async (s, e) => await HandleActionInvokedAsync(action.Id, null);
            }

            if (_buttonPanel.Controls.Count > 0)
            {
                button.Margin = new Padding(8, 0, 0, 0);
            }

            _buttonPanel.Controls.Add(button);
        }

        UpdateActionAreaVisibility();
    }

    private async Task HandleActionInvokedAsync(string actionId, string? optionId)
    {
        if (_isBusy)
        {
            return;
        }

        var action = _actions.FirstOrDefault(a => string.Equals(a.Id, actionId, StringComparison.OrdinalIgnoreCase));
        if (action is null)
        {
            return;
        }

        var text = _inputTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            UpdateMessage("Enter or paste text before selecting an action.");
            return;
        }

        SetBusyState(true, $"Running {action.DisplayName}…");
        _responseTextBox.Text = string.Empty;

        try
        {
            var result = await action.ExecuteAsync(text, optionId, CancellationToken.None);
            if (!result.Success)
            {
                UpdateMessage(result.Message ?? "Action failed.");
                return;
            }

            if (string.IsNullOrWhiteSpace(result.ReplacementText))
            {
                UpdateMessage(result.Message ?? "The selected action did not return any text.");
                return;
            }

            _responseTextBox.Text = result.ReplacementText;
            _responseTextBox.SelectionStart = 0;
            _responseTextBox.SelectionLength = _responseTextBox.TextLength;
            UpdateMessage(result.SuccessMessage ?? $"{action.DisplayName} applied. Review the response below.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Action '{actionId}' failed", ex);
            UpdateMessage("Unable to complete the selected action.");
        }
        finally
        {
            SetBusyState(false, null);
        }
    }

    private void UpdateMessage(string text)
    {
        if (!IsDisposed)
        {
            _messageLabel.Text = text;
        }
    }

    private void SetBusyState(bool isBusy, string? loadingText)
    {
        if (IsDisposed)
        {
            return;
        }

        _isBusy = isBusy;
        UseWaitCursor = isBusy;
        _inputTextBox.ReadOnly = isBusy;
        _buttonPanel.Enabled = !isBusy;
        _loadingPanel.Visible = isBusy;
        _buttonPanel.Visible = !isBusy;

        if (!string.IsNullOrWhiteSpace(loadingText))
        {
            _loadingLabel.Text = loadingText!;
        }
    }

    private void UpdateActionAreaVisibility()
    {
        if (IsDisposed)
        {
            return;
        }

        _buttonPanel.Visible = !_isBusy;
        _loadingPanel.Visible = _isBusy;
    }
}
