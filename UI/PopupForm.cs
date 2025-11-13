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
    private readonly FlowLayoutPanel _buttonPanel;
    private readonly Button _closeButton;
    private readonly List<ContextMenuStrip> _optionMenus = new();
    private TaskCompletionSource<bool>? _confirmationCompletion;

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
            RowCount = 2,
            BackColor = Color.White,
            Padding = new Padding(20, 18, 20, 20),
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
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

        _messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Text = string.IsNullOrWhiteSpace(message) ? "Choose an action for the selected text." : message,
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

        _buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 12, 0, 0),
            Visible = false
        };

        layout.Controls.Add(_buttonPanel, 0, 1);
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
            CompleteConfirmation(false);
        }

        base.OnFormClosing(e);
    }

    public void SetActions(IEnumerable<PopupActionDescriptor> actions)
    {
        ClearActionButtons();
        var descriptors = actions?.ToList() ?? new List<PopupActionDescriptor>();
        if (descriptors.Count == 0)
        {
            return;
        }

        foreach (var descriptor in descriptors)
        {
            var button = descriptor.IsPrimary
                ? CreatePrimaryActionButton(descriptor.Label)
                : CreateSecondaryActionButton(descriptor.Label);

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

        _buttonPanel.Visible = true;
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
        _buttonPanel.Visible = false;
        _buttonPanel.Enabled = true;
    }

    public Task<bool> ShowReplacementPreviewAsync(string replacementText, string approveButtonText, string cancelButtonText = "Cancel")
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(PopupForm));

        if (_confirmationCompletion is not null)
            throw new InvalidOperationException("A confirmation is already in progress.");

        _confirmationCompletion = new TaskCompletionSource<bool>();

        _messageLabel.MaximumSize = new Size(480, 0);
        UpdateMessage(replacementText);

        _buttonPanel.Controls.Clear();
        _buttonPanel.Enabled = true;

        var cancelButton = CreateSecondaryActionButton(cancelButtonText);
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        cancelButton.Click += (s, e) => CompleteConfirmation(false);

        var approveButton = CreatePrimaryActionButton(approveButtonText);
        approveButton.Click += (s, e) => CompleteConfirmation(true);

        _buttonPanel.Controls.Add(approveButton);
        _buttonPanel.Controls.Add(cancelButton);
        _buttonPanel.Visible = true;

        PerformLayout();
        return _confirmationCompletion.Task;
    }

    private void CompleteConfirmation(bool accepted)
    {
        if (_confirmationCompletion is null)
            return;

        _buttonPanel.Enabled = false;
        _confirmationCompletion.TrySetResult(accepted);
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

        UseWaitCursor = isBusy;
        Cursor = isBusy ? Cursors.WaitCursor : Cursors.Default;
        _buttonPanel.Enabled = !isBusy;
        _closeButton.Enabled = !isBusy;
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

    private static Button CreatePrimaryActionButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.BackColor = Color.FromArgb(46, 90, 165);
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(61, 113, 203);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(39, 78, 143);
        return button;
    }

    private static Button CreateSecondaryActionButton(string text)
    {
        var button = CreateBaseActionButton(text);
        button.BackColor = Color.FromArgb(237, 240, 247);
        button.ForeColor = Color.FromArgb(46, 90, 165);
        button.FlatAppearance.BorderColor = Color.FromArgb(209, 216, 232);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(227, 233, 246);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(215, 224, 243);
        return button;
    }

    private static Button CreateBaseActionButton(string text)
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(14, 7, 14, 7),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Text = text,
            TabStop = false
        };

        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
