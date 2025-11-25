using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using GlobalTextHelper.Domain.Responding;

namespace GlobalTextHelper.UI;

public sealed class PopupForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Label _messageLabel;
    private readonly TextBox _selectionTextBox;
    private readonly TableLayoutPanel _rewriteContainer;
    private readonly FlowLayoutPanel _modeSelectionPanel;
    private readonly FlowLayoutPanel _buttonPanel;
    private readonly TableLayoutPanel _respondContainer;
    private readonly FlowLayoutPanel _respondPanel;
    private readonly Label _respondStatusLabel;
    private readonly FlowLayoutPanel _respondButtonPanel;
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

    public PopupForm(string message, int autohideMs, string selectionText)
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        ShowInTaskbar = false;
        BackColor = Theme.SurfaceColor;
        Opacity = 0.98;
        Padding = new Padding(Theme.PaddingSmall);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.BackgroundColor,
            Padding = new Padding(Theme.PaddingLarge),
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
            Font = Theme.HeaderFont,
            ForeColor = Theme.PrimaryColor,
            Margin = new Padding(0, 0, 0, 4)
        };

        _defaultMessage = string.IsNullOrWhiteSpace(message)
            ? "Välj vad du vill göra med den markerade texten."
            : message;

        _messageLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(Theme.PopupWidth, 0),
            Text = _defaultMessage,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
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
            ForeColor = Theme.TextMutedColor,
            Font = Theme.BodyFont,
            Margin = new Padding(12, 0, 0, 0),
            Padding = new Padding(6, 2, 6, 2),
            Cursor = Cursors.Hand,
            TabStop = false,
            UseVisualStyleBackColor = false
        };

        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.FlatAppearance.MouseOverBackColor = Theme.SecondaryHoverColor;
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
            RowCount = 4,
            Margin = new Padding(0)
        };

        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contentPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(contentPanel, 0, 1);

        _selectionTextBox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            BackColor = Theme.SurfaceColor,
            BorderStyle = BorderStyle.None,
            MinimumSize = new Size(Theme.PopupWidth, 80),
            MaximumSize = new Size(Theme.PopupWidth, 600),
            Size = new Size(Theme.PopupWidth, 100),
            Margin = new Padding(0, 12, 0, 0),
            Text = selectionText ?? string.Empty,
            AcceptsReturn = true,
            AcceptsTab = true,
            Dock = DockStyle.Fill
        };

        contentPanel.Controls.Add(_selectionTextBox, 0, 0);
        AdjustSelectionTextBoxHeight();

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

        var rewriteButton = ActionButtonFactory.CreatePrimaryActionButton("Skriv om markerad text");
        rewriteButton.Click += (_, __) => ShowRewriteActions();

        var respondButton = ActionButtonFactory.CreateSecondaryActionButton("Svara på markerad text");
        respondButton.Margin = new Padding(8, 0, 0, 0);
        respondButton.Click += async (_, __) => await BeginRespondFlowAsync();

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

        var rewriteBackButton = ActionButtonFactory.CreateBackNavigationButton("< Tillbaka");
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

        _respondStatusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(Theme.PopupWidth, 0),
            Text = "Välj hur du vill svara.",
            Font = Theme.BodyFont,
            ForeColor = Theme.TextColor,
            Margin = new Padding(0, 0, 0, 8)
        };

        _respondButtonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };

        _respondPanel.Controls.Add(_respondStatusLabel);
        _respondPanel.Controls.Add(_respondButtonPanel);

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

        var respondBackButton = ActionButtonFactory.CreateBackNavigationButton("< Tillbaka");
        respondBackButton.Click += (_, __) => ShowModeSelection();
        respondBackRow.Controls.Add(respondBackButton);

        _respondContainer.Controls.Add(respondBackRow, 0, 0);
        _respondContainer.Controls.Add(_respondPanel, 0, 1);

        contentPanel.Controls.Add(_modeSelectionPanel, 0, 1);
        contentPanel.Controls.Add(_rewriteContainer, 0, 2);
        contentPanel.Controls.Add(_respondContainer, 0, 3);

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
            Text = "Väntar på ett svar…",
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = Theme.TextMutedColor,
            Margin = new Padding(0, 2, 0, 0)
        };

        _loadingPanel.Controls.Add(_loadingIndicator);
        _loadingPanel.Controls.Add(_loadingLabel);

        layout.Controls.Add(_loadingPanel, 0, 2);
        Controls.Add(layout);

        Paint += (s, e) =>
        {
            using var backgroundPen = new Pen(Theme.BorderColor);
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
    public event Func<string, Task>? RespondRequested;
    public event EventHandler<RespondSuggestionAppliedEventArgs>? RespondSuggestionApplied;

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

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;
        const int HTCAPTION = 2;

        base.WndProc(ref m);

        if (m.Msg == WM_NCHITTEST && (int)m.Result == HTCLIENT)
        {
            // Treat all client clicks as caption to allow dragging the popup.
            m.Result = (IntPtr)HTCAPTION;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        EnsureWithinScreen();
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
        _buttonPanel.FlowDirection = FlowDirection.TopDown;
        _buttonPanel.WrapContents = false;

        foreach (var descriptor in _rewriteActionDescriptors)
        {
            if (IsRewriteDescriptor(descriptor) && descriptor.Options.Count > 0)
            {
                _buttonPanel.Controls.Add(BuildRewriteOptionLayout(descriptor));
                continue;
            }

            var button = CreateActionButton(descriptor);
            if (_buttonPanel.Controls.Count > 0)
            {
                button.Margin = new Padding(0, 8, 0, 0);
            }

            _buttonPanel.Controls.Add(button);
        }

        _currentView = PopupViewMode.RewriteActions;
        UpdateMessage("Skriv om din text.");
        UpdateActionAreaVisibility();
    }

    private static bool IsRewriteDescriptor(PopupActionDescriptor descriptor)
    {
        return string.Equals(descriptor.Id, "rewrite", StringComparison.OrdinalIgnoreCase);
    }

    private Control BuildRewriteOptionLayout(PopupActionDescriptor descriptor)
    {
        var wrapper = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 4, 0, 0)
        };

        wrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        wrapper.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            AutoSize = true,
            Text = "Skriv om din text",
            Font = Theme.HeaderFont,
            ForeColor = Theme.TextColor,
            Margin = new Padding(0, 0, 0, 8)
        };

        var grid = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 4,
            Margin = new Padding(0)
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        grid.Controls.Add(CreateRewriteHeadingLabel("Stil"), 0, 0);
        grid.Controls.Add(CreateRewriteHeadingLabel("Längd"), 1, 0);
        grid.Controls.Add(CreateRewriteHeadingLabel("Stavning"), 2, 0);

        AddRewriteButtonIfAvailable(grid, 0, 1, descriptor, "formal", "Formellt");
        AddRewriteButtonIfAvailable(grid, 0, 2, descriptor, "casual", "Avslappnat");
        AddRewriteButtonIfAvailable(grid, 1, 1, descriptor, "minimal", "Minimalt");
        AddRewriteButtonIfAvailable(grid, 1, 2, descriptor, "shorter", "Kortare");
        AddRewriteButtonIfAvailable(grid, 1, 3, descriptor, "longer", "Längre");
        AddRewriteButtonIfAvailable(grid, 2, 1, descriptor, "spelling", "Fixa stavfel");

        wrapper.Controls.Add(title, 0, 0);
        wrapper.Controls.Add(grid, 0, 1);
        return wrapper;
    }

    private static Label CreateRewriteHeadingLabel(string text)
    {
        return new Label
        {
            AutoSize = true,
            Text = text,
            Font = Theme.ButtonFont,
            ForeColor = Theme.TextColor,
            Margin = new Padding(0, 0, 0, 6)
        };
    }

    private void AddRewriteButtonIfAvailable(
        TableLayoutPanel grid,
        int column,
        int row,
        PopupActionDescriptor descriptor,
        string optionId,
        string label)
    {
        if (!DescriptorHasOption(descriptor, optionId))
        {
            return;
        }

        var button = CreateRewriteOptionButton(label, optionId, descriptor.Id, column == grid.ColumnCount - 1);
        grid.Controls.Add(button, column, row);
    }

    private static bool DescriptorHasOption(PopupActionDescriptor descriptor, string optionId)
    {
        return descriptor.Options.Any(o => string.Equals(o.Id, optionId, StringComparison.OrdinalIgnoreCase));
    }

    private Button CreateRewriteOptionButton(string label, string optionId, string actionId, bool isLastColumn)
    {
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 8, 12, 8),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Text = label,
            Font = Theme.ButtonFont,
            BackColor = Color.FromArgb(219, 234, 254), // Blue 100
            ForeColor = Color.FromArgb(37, 99, 235),   // Blue 600
            Margin = isLastColumn
                ? new Padding(0, 4, 0, 4)
                : new Padding(0, 4, 12, 4),
            TabStop = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(191, 219, 254); // Blue 200
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(147, 197, 253); // Blue 300
        button.Click += async (s, e) => await RaiseActionInvokedAsync(actionId, optionId);
        return button;
    }

    private Button CreateActionButton(PopupActionDescriptor descriptor)
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

        return button;
    }

    private void ShowRespondView()
    {
        ClearActionButtons();
        _currentView = PopupViewMode.Respond;
        UpdateMessage("Svara på den markerade texten.");
        UpdateActionAreaVisibility();
    }

    private async Task BeginRespondFlowAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        ShowRespondView();
        SetRespondStatus("Tar fram svarsidéer…");
        ClearRespondButtons();

        var handler = RespondRequested;
        if (handler is null)
        {
            SetRespondStatus("Inga svarsförslag är tillgängliga.");
            return;
        }

        try
        {
            await handler(_selectionTextBox.Text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            SetRespondStatus("Det gick inte att läsa in svarsalternativ.");
        }
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
            await handler(new PopupActionInvokedEventArgs(actionId, optionId, _selectionTextBox.Text));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void SetSelectionText(string text)
    {
        if (IsDisposed)
        {
            return;
        }

        _selectionTextBox.Text = text ?? string.Empty;
        _selectionTextBox.SelectionStart = _selectionTextBox.TextLength;
        _selectionTextBox.SelectionLength = 0;
        _selectionTextBox.ScrollToCaret();
        AdjustSelectionTextBoxHeight();
    }

    private void AdjustSelectionTextBoxHeight()
    {
        if (_selectionTextBox.IsDisposed)
        {
            return;
        }

        var width = _selectionTextBox.ClientSize.Width;
        if (width <= 0)
        {
            width = Theme.PopupWidth - SystemInformation.VerticalScrollBarWidth - 4;
        }

        int newHeight;
        try
        {
            var size = TextRenderer.MeasureText(
                _selectionTextBox.Text,
                _selectionTextBox.Font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            newHeight = size.Height + 24;
        }
        catch (Exception)
        {
            // Fallback if measurement fails (e.g. text too long for GDI)
            newHeight = 100;
        }

        const int minHeight = 80;
        const int maxHeight = 500;

        newHeight = Math.Max(minHeight, Math.Min(newHeight, maxHeight));
        _selectionTextBox.Height = newHeight;
        EnsureWithinScreen();
    }

    public void SetRespondSuggestions(IEnumerable<ResponseSuggestion> suggestions)
    {
        if (IsDisposed)
        {
            return;
        }

        ShowRespondView();
        ClearRespondButtons();

        var list = suggestions?.ToList() ?? new List<ResponseSuggestion>();
        if (list.Count == 0)
        {
            SetRespondStatus("Inga svarsförslag returnerades.");
            UpdateActionAreaVisibility();
            return;
        }

        foreach (var suggestion in list)
        {
            var button = CreateRespondButton(suggestion);
            _respondButtonPanel.Controls.Add(button);
        }

        UpdateActionAreaVisibility();
    }

    public void SetRespondStatus(string text)
    {
        if (!IsDisposed)
        {
            _respondStatusLabel.Text = text;
        }
    }

    private void ClearRespondButtons()
    {
        if (IsDisposed)
        {
            return;
        }

        foreach (Control control in _respondButtonPanel.Controls.Cast<Control>().ToList())
        {
            control.Dispose();
        }

        _respondButtonPanel.Controls.Clear();
    }

    private Button CreateRespondButton(ResponseSuggestion suggestion)
    {
        var palette = GetRespondPalette(suggestion.Tone);
        var button = new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = BuildRespondButtonText(suggestion),
            Padding = new Padding(14, 8, 14, 8),
            MaximumSize = new Size(Theme.PopupWidth, 0),
            Margin = new Padding(_respondButtonPanel.Controls.Count > 0 ? 8 : 0, 0, 0, 8),
            BackColor = palette.Back,
            ForeColor = palette.Fore,
            TabStop = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = palette.Hover;
        button.FlatAppearance.MouseDownBackColor = palette.Down;
        button.Click += (s, e) => ApplyRespondSuggestion(suggestion);
        return button;
    }

    private static (Color Back, Color Hover, Color Down, Color Fore) GetRespondPalette(ResponseTone tone)
    {
        return tone switch
        {
            ResponseTone.Affirmative =>
                (Color.FromArgb(34, 197, 94), Color.FromArgb(22, 163, 74), Color.FromArgb(21, 128, 61), Color.White), // Green 500/600/700
            ResponseTone.Negative =>
                (Color.FromArgb(239, 68, 68), Color.FromArgb(220, 38, 38), Color.FromArgb(185, 28, 28), Color.White), // Red 500/600/700
            _ =>
                (Theme.SecondaryColor, Theme.SecondaryHoverColor, Theme.SecondaryPressedColor, Theme.SecondaryTextColor)
        };
    }

    private static string BuildRespondButtonText(ResponseSuggestion suggestion)
    {
        string snippet = string.IsNullOrWhiteSpace(suggestion.Snippet)
            ? GetDefaultSnippet(suggestion.Tone)
            : suggestion.Snippet.Trim();

        if (snippet.Length > 70)
        {
            snippet = snippet[..67].TrimEnd() + "…";
        }

        return snippet;
    }

    private static string GetDefaultSnippet(ResponseTone tone)
    {
        return tone switch
        {
            ResponseTone.Affirmative => "Bekräftar ja",
            ResponseTone.Negative => "Tackar nej vänligt",
            _ => "Ber om mer information"
        };
    }

    private void ApplyRespondSuggestion(ResponseSuggestion suggestion)
    {
        if (IsDisposed)
        {
            return;
        }

        StopAutoClose();
        SetSelectionText(suggestion.FullResponse);
        SetRespondStatus("Svaret har lagts in nedan. Redigera eller kopiera innan du skickar.");
        UpdateMessage("Ett utkast till svar har lagts in nedan.");
        RespondSuggestionApplied?.Invoke(this, new RespondSuggestionAppliedEventArgs(suggestion.Tone));
    }

    public string GetSelectionText()
    {
        return IsDisposed ? string.Empty : _selectionTextBox.Text;
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
        string copyButtonText = "Kopiera till urklipp",
        string cancelButtonText = "Avbryt")
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(PopupForm));

        if (_confirmationCompletion is not null)
            throw new InvalidOperationException("A confirmation is already in progress.");

        _confirmationCompletion = new TaskCompletionSource<ReplacementPreviewResult>();

        _messageLabel.MaximumSize = new Size(480, 0);
        UpdateMessage("Granska eller justera den uppdaterade texten nedan.");
        SetSelectionText(replacementText);

        _buttonPanel.Controls.Clear();
        _buttonPanel.Enabled = true;
        _buttonPanel.FlowDirection = FlowDirection.LeftToRight;
        _buttonPanel.WrapContents = false;

        var copyButton = ActionButtonFactory.CreatePrimaryActionButton(copyButtonText);
        copyButton.Click += (s, e) => CompleteConfirmation(ReplacementPreviewResult.CopyToClipboard);

        var cancelButton = ActionButtonFactory.CreateSecondaryActionButton(cancelButtonText);
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        cancelButton.Click += (s, e) => CompleteConfirmation(ReplacementPreviewResult.Cancel);

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
        EnsureWithinScreen();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer?.Dispose();
            foreach (var menu in _optionMenus)
            {
                menu.Dispose();
            }
            _optionMenus.Clear();
        }
        base.Dispose(disposing);
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
            EnsureWithinScreen();
        }));
    }

    private void EnsureWithinScreen()
    {
        if (IsDisposed)
        {
            return;
        }

        var screen = Screen.FromPoint(Location).WorkingArea;
        int x = Math.Min(Location.X, screen.Right - Width - 8);
        int y = Math.Min(Location.Y, screen.Bottom - Height - 8);
        x = Math.Max(screen.Left + 8, x);
        y = Math.Max(screen.Top + 8, y);
        Location = new Point(x, y);
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
