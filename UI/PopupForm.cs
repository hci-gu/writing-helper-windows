using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlobalTextHelper.UI;

public sealed class PopupForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label _messageLabel;
    private readonly TableLayoutPanel _rewriteContainer;
    private readonly FlowLayoutPanel _modeSelectionPanel;
    private readonly FlowLayoutPanel _buttonPanel;
    private readonly TableLayoutPanel _respondContainer;
    private readonly FlowLayoutPanel _respondPanel;
    private readonly FlowLayoutPanel _loadingPanel;
    private readonly ProgressBar _loadingIndicator;
    private readonly Label _loadingLabel;
    private readonly Button _closeButton;
    private readonly List<ContextMenuStrip> _optionMenus = new();
    private readonly List<PopupActionDescriptor> _rewriteActionDescriptors = new();
    private readonly string _defaultMessage;
    private PopupViewMode _currentView = PopupViewMode.ModeSelection;
    private TaskCompletionSource<ReplacementPreviewResult>? _confirmationCompletion;
    private bool _isBusy;

    public PopupForm(string message, int autohideMs)
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(243, 245, 250);
        Opacity = 0.98;
        Padding = new Padding(16, 16, 16, 18);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.White,
            Padding = new Padding(20, 18, 20, 20),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 6)
        };

        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerTextPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            RowCount = 2,
        };

        headerTextPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerTextPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "Writing Helper",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(46, 90, 165),
            Margin = new Padding(0, 0, 0, 2)
        };

        _defaultMessage = string.IsNullOrWhiteSpace(message)
            ? "Choose what to do with the selected text."
            : message;

        _messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Text = _defaultMessage,
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(60, 60, 60),
            Margin = new Padding(0)
        };

        headerTextPanel.Controls.Add(titleLabel, 0, 0);
        headerTextPanel.Controls.Add(_messageLabel, 0, 1);

        headerPanel.Controls.Add(headerTextPanel, 0, 0);

        _closeButton = new Button
        {
            Text = "✕",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(120, 120, 120),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(12, 0, 0, 0),
            Padding = new Padding(6, 2, 6, 2),
            Cursor = Cursors.Hand,
            TabStop = false,
            UseVisualStyleBackColor = false
        };

        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 237, 250);
        _closeButton.Click += (s, e) =>
        {
            StopAutoClose();
            Close();
        };

        headerPanel.Controls.Add(_closeButton, 1, 0);
        layout.Controls.Add(headerPanel, 0, 0);

        var contentPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };

        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(contentPanel, 0, 1);

        _modeSelectionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
            Visible = true
        };

        var rewriteButton = ActionButtonFactory.CreatePrimaryActionButton("Rewrite selected text");
        rewriteButton.Click += (_, __) => ShowRewriteActions();

        var respondButton = ActionButtonFactory.CreateSecondaryActionButton("Respond to selected text");
        respondButton.Margin = new Padding(8, 0, 0, 0);
        respondButton.Click += (_, __) => ShowRespondView();

        _modeSelectionPanel.Controls.Add(rewriteButton);
        _modeSelectionPanel.Controls.Add(respondButton);

        _buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 0),
            Visible = false
        };

        var rewriteBackRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0)
        };

        var rewriteBackButton = ActionButtonFactory.CreateBackNavigationButton("< Back");
        rewriteBackButton.Click += (_, __) => ShowModeSelection();
        rewriteBackRow.Controls.Add(rewriteBackButton);

        _rewriteContainer = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Visible = false
        };

        _rewriteContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rewriteContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _rewriteContainer.Controls.Add(rewriteBackRow, 0, 0);
        _rewriteContainer.Controls.Add(_buttonPanel, 0, 1);

        _respondPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(0, 6, 0, 0),
            Visible = false
        };

        var respondInfo = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(480, 0),
            Text = "Response options are coming soon.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(60, 60, 60),
            Margin = new Padding(0, 0, 0, 8)
        };

        _respondPanel.Controls.Add(respondInfo);

        _respondContainer = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Visible = false
        };

        _respondContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _respondContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var respondBackRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0)
        };

        var respondBackButton = ActionButtonFactory.CreateBackNavigationButton("< Back");
        respondBackButton.Click += (_, __) => ShowModeSelection();
        respondBackRow.Controls.Add(respondBackButton);

        _respondContainer.Controls.Add(respondBackRow, 0, 0);
        _respondContainer.Controls.Add(_respondPanel, 0, 1);

        contentPanel.Controls.Add(_modeSelectionPanel, 0, 0);
        contentPanel.Controls.Add(_rewriteContainer, 0, 1);
        contentPanel.Controls.Add(_respondContainer, 0, 2);

        _loadingPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 12, 0, 0),
            Visible = false
        };

        _loadingIndicator = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 25,
            Size = new Size(140, 14),
            Margin = new Padding(0, 4, 12, 0)
        };

        _loadingLabel = new Label
        {
            AutoSize = true,
            Text = "Waiting for a response…",
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Color.FromArgb(90, 90, 90),
            Margin = new Padding(0, 2, 0, 0)
        };

        _loadingPanel.Controls.Add(_loadingIndicator);
        _loadingPanel.Controls.Add(_loadingLabel);

        layout.Controls.Add(_loadingPanel, 0, 2);
        Controls.Add(layout);

        Paint += (s, e) =>
        {
            using var backgroundPen = new Pen(Color.FromArgb(216, 224, 244));
            e.Graphics.DrawRectangle(backgroundPen, 0, 0, Width - 1, Height - 1);
        };

        _timer = new System.Windows.Forms.Timer { Interval = autohideMs };
        if (autohideMs > 0)
        {
            _timer.Tick += (s, e) => Close();
            _timer.Start();
        }

        Click += (_, __) => Close();
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    public event Func<PopupActionInvokedEventArgs, Task>? ActionInvoked;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_confirmationCompletion is not null)
        {
            CompleteConfirmation(ReplacementPreviewResult.Cancel);
        }

        base.OnFormClosing(e);
    }

    public void SetActions(IEnumerable<PopupActionDescriptor> actions)
    {
        ClearActionButtons();
        var descriptors = actions?.ToList() ?? new List<PopupActionDescriptor>();
        _rewriteActionDescriptors.Clear();
        _rewriteActionDescriptors.AddRange(descriptors);
        ShowModeSelection();
    }

    private void ShowModeSelection()
    {
        if (_currentView == PopupViewMode.RewriteActions)
        {
            ClearActionButtons();
        }

        _currentView = PopupViewMode.ModeSelection;
        UpdateMessage(_defaultMessage);
        UpdateActionAreaVisibility();
    }

    private void ShowRewriteActions()
    {
        if (_rewriteActionDescriptors.Count == 0)
        {
            return;
        }

        ClearActionButtons();

        foreach (var descriptor in _rewriteActionDescriptors)
        {
            var button = descriptor.IsPrimary
                ? ActionButtonFactory.CreatePrimaryActionButton(descriptor.Label)
                : ActionButtonFactory.CreateSecondaryActionButton(descriptor.Label);

            if (descriptor.Options.Count > 0)
            {
                var menu = new ContextMenuStrip();
                foreach (var option in descriptor.Options)
                {
                    var item = new ToolStripMenuItem(option.Label) { Tag = option.Id };
                    item.Click += async (s, e) => await RaiseActionInvokedAsync(descriptor.Id, option.Id);
                    menu.Items.Add(item);
                }

                button.Click += (s, e) => menu.Show(button, new Point(0, button.Height));
                _optionMenus.Add(menu);
            }
            else
            {
                button.Click += async (s, e) => await RaiseActionInvokedAsync(descriptor.Id, null);
            }

            if (_buttonPanel.Controls.Count > 0)
            {
                button.Margin = new Padding(8, 0, 0, 0);
            }

            _buttonPanel.Controls.Add(button);
        }

        _currentView = PopupViewMode.RewriteActions;
        UpdateMessage("Choose a rewrite option for the selected text.");
        UpdateActionAreaVisibility();
    }

    private void ShowRespondView()
    {
        ClearActionButtons();
        _currentView = PopupViewMode.Respond;
        UpdateMessage("Respond to the selected text (coming soon).");
        UpdateActionAreaVisibility();
    }

    private async Task RaiseActionInvokedAsync(string actionId, string? optionId)
    {
        var handler = ActionInvoked;
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(new PopupActionInvokedEventArgs(actionId, optionId));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void UpdateMessage(string text)
    {
        if (!IsDisposed)
        {
            _messageLabel.Text = text;
            PerformLayout();
        }
    }

    public void ClearActionButtons()
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (var menu in _optionMenus)
        {
            menu.Dispose();
        }

        _optionMenus.Clear();
        _buttonPanel.Controls.Clear();
        _buttonPanel.Enabled = true;
        UpdateActionAreaVisibility();
    }

    public Task<ReplacementPreviewResult> ShowReplacementPreviewAsync(
        string replacementText,
        string approveButtonText,
        string copyButtonText = "Copy to Clipboard",
        string cancelButtonText = "Cancel")
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(PopupForm));

        if (_confirmationCompletion is not null)
            throw new InvalidOperationException("A confirmation is already in progress.");

        _confirmationCompletion = new TaskCompletionSource<ReplacementPreviewResult>();

        _messageLabel.MaximumSize = new Size(480, 0);
        UpdateMessage(replacementText);

        _buttonPanel.Controls.Clear();
        _buttonPanel.Enabled = true;

        var cancelButton = ActionButtonFactory.CreateSecondaryActionButton(cancelButtonText);
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        cancelButton.Click += (s, e) => CompleteConfirmation(ReplacementPreviewResult.Cancel);

        var approveButton = ActionButtonFactory.CreatePrimaryActionButton(approveButtonText);
        approveButton.Click += (s, e) => CompleteConfirmation(ReplacementPreviewResult.Accept);

        var copyButton = ActionButtonFactory.CreateSecondaryActionButton(copyButtonText);
        copyButton.Margin = new Padding(8, 0, 0, 0);
        copyButton.Click += (s, e) => CompleteConfirmation(ReplacementPreviewResult.CopyToClipboard);

        _buttonPanel.Controls.Add(approveButton);
        _buttonPanel.Controls.Add(copyButton);
        _buttonPanel.Controls.Add(cancelButton);
        UpdateActionAreaVisibility();

        PerformLayout();
        return _confirmationCompletion.Task;
    }

    private void CompleteConfirmation(ReplacementPreviewResult result)
    {
        if (_confirmationCompletion is null)
            return;

        _buttonPanel.Enabled = false;
        _confirmationCompletion.TrySetResult(result);
        _confirmationCompletion = null;
    }

    public void StopAutoClose()
    {
        if (!IsDisposed)
        {
            _timer.Stop();
        }
    }

    public void RestartAutoClose(int milliseconds)
    {
        if (!IsDisposed)
        {
            _timer.Stop();
            _timer.Interval = milliseconds;
            _timer.Start();
        }
    }

    public void SetBusyState(bool isBusy)
    {
        if (IsDisposed)
            return;

        _isBusy = isBusy;
        UseWaitCursor = isBusy;
        Cursor = isBusy ? Cursors.WaitCursor : Cursors.Default;
        _buttonPanel.Enabled = !isBusy;
        _modeSelectionPanel.Enabled = !isBusy;
        _rewriteContainer.Enabled = !isBusy;
        _respondContainer.Enabled = !isBusy;
        _respondPanel.Enabled = !isBusy;
        _closeButton.Enabled = !isBusy;
        UpdateActionAreaVisibility();
    }

    private void UpdateActionAreaVisibility()
    {
        var canShowContent = !_isBusy;
        _modeSelectionPanel.Visible = canShowContent && _currentView == PopupViewMode.ModeSelection;
        var showRewrite = canShowContent && _currentView == PopupViewMode.RewriteActions;
        _rewriteContainer.Visible = showRewrite;
        _buttonPanel.Visible = showRewrite && _buttonPanel.Controls.Count > 0;
        var showRespond = canShowContent && _currentView == PopupViewMode.Respond;
        _respondContainer.Visible = showRespond;
        _respondPanel.Visible = showRespond;
        _loadingPanel.Visible = _isBusy;
        PerformLayout();
    }

    public void ShowNear(Point initialLocation)
    {
        StartPosition = FormStartPosition.Manual;
        Location = initialLocation;
        Show();

        BeginInvoke(new Action(() =>
        {
            var cursor = Cursor.Position;
            var screen = Screen.FromPoint(cursor).WorkingArea;
            var size = Size;
            int nx = Math.Min(initialLocation.X, screen.Right - size.Width - 8);
            int ny = Math.Min(initialLocation.Y, screen.Bottom - size.Height - 8);
            nx = Math.Max(screen.Left + 8, nx);
            ny = Math.Max(screen.Top + 8, ny);
            Location = new Point(nx, ny);
        }));
    }

}

internal enum PopupViewMode
{
    ModeSelection,
    RewriteActions,
    Respond
}

public enum ReplacementPreviewResult
{
    Accept,
    CopyToClipboard,
    Cancel
}


