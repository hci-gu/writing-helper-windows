using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class PopupForm : Form
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Label _label;
        private readonly Button? _simplifyButton;
        private readonly Button? _rewriteButton;
        private readonly Button _closeButton;
        private readonly ContextMenuStrip? _rewriteMenu;
        private readonly Dictionary<string, string>? _rewriteStyleDisplayNames;

        public event Func<PopupForm, Task>? SimplifyRequested;
        public event Func<PopupForm, string, Task>? RewriteRequested;

        public PopupForm(string text, int autohideMs, bool showSimplifyButton = false, bool showRewriteButton = false)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(248, 249, 252);
            Opacity = 0.98;
            Padding = new Padding(14, 14, 14, 16);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = (showSimplifyButton || showRewriteButton) ? 2 : 1,
                BackColor = Color.White,
                Padding = new Padding(16, 14, 16, 16),
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (showSimplifyButton || showRewriteButton)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            var headerPanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = new Padding(0, 0, 0, 8)
            };

            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(450, 0), // wrap long lines
                Text = text,
                Font = new Font("Segoe UI", 9.75f),
                ForeColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0)
            };

            headerPanel.Controls.Add(_label, 0, 0);

            _closeButton = new Button
            {
                Text = "✕",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(130, 130, 130),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(12, 0, 0, 0),
                Padding = new Padding(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                TabStop = false,
                UseVisualStyleBackColor = false
            };

            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 235, 235);
            _closeButton.Click += (s, e) =>
            {
                StopAutoClose();
                Close();
            };

            headerPanel.Controls.Add(_closeButton, 1, 0);

            layout.Controls.Add(headerPanel, 0, 0);

            if (showSimplifyButton || showRewriteButton)
            {
                var buttonPanel = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    Margin = new Padding(0, 8, 0, 0)
                };

                if (showSimplifyButton)
                {
                    _simplifyButton = CreatePrimaryActionButton("Simplify & Replace");
                    if (showRewriteButton)
                    {
                        _simplifyButton.Margin = new Padding(0, 0, 8, 0);
                    }

                    _simplifyButton.Click += async (s, e) => await HandleSimplifyClickAsync();
                    buttonPanel.Controls.Add(_simplifyButton);
                }

                if (showRewriteButton)
                {
                    _rewriteButton = CreateSecondaryActionButton("Rewrite…");

                    _rewriteMenu = new ContextMenuStrip();
                    _rewriteStyleDisplayNames = new Dictionary<string, string>
                    {
                        { "minimal", "Minimal" },
                        { "spelling", "Fix Spelling" },
                        { "shorter", "Shorter" },
                        { "longer", "Longer" },
                        { "formal", "Formal" },
                        { "casual", "Casual" }
                    };

                    foreach (var kvp in _rewriteStyleDisplayNames)
                    {
                        var item = new ToolStripMenuItem(kvp.Value) { Tag = kvp.Key };
                        item.Click += async (s, e) =>
                        {
                            _rewriteMenu?.Close();
                            if (s is ToolStripMenuItem menuItem && menuItem.Tag is string styleKey)
                            {
                                await HandleRewriteStyleSelectedAsync(styleKey);
                            }
                        };
                        _rewriteMenu.Items.Add(item);
                    }

                    _rewriteButton.Click += (s, e) =>
                    {
                        if (_rewriteMenu is not null)
                        {
                            _rewriteMenu.Show(_rewriteButton, new Point(0, _rewriteButton.Height));
                        }
                    };

                    buttonPanel.Controls.Add(_rewriteButton);
                }

                layout.Controls.Add(buttonPanel, 0, 1);
            }

            Controls.Add(layout);

            // Subtle border
            Paint += (s, e) =>
            {
                using var backgroundPen = new Pen(Color.FromArgb(228, 232, 245));
                e.Graphics.DrawRectangle(backgroundPen, 0, 0, Width - 1, Height - 1);
            };

            _timer = new System.Windows.Forms.Timer { Interval = autohideMs };
            if (autohideMs > 0)
            {
                _timer.Tick += (s, e) => Close();
                _timer.Start();
            }

            // Close on click or ESC
            Click += (_, __) => Close();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        // Don’t steal focus
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

        public void UpdateMessage(string text)
        {
            if (!IsDisposed)
            {
                _label.Text = text;
            }
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
            if (_simplifyButton is not null)
            {
                _simplifyButton.Enabled = !isBusy;
            }
            if (_rewriteButton is not null)
            {
                _rewriteButton.Enabled = !isBusy;
            }
            _closeButton.Enabled = !isBusy;
        }

        private async Task HandleSimplifyClickAsync()
        {
            var handler = SimplifyRequested;
            if (handler is null)
                return;

            StopAutoClose();
            UpdateMessage("Simplifying selection…");
            SetBusyState(true);

            try
            {
                await handler(this);
            }
            catch (Exception ex)
            {
                UpdateMessage($"Failed to simplify: {ex.Message}");
                RestartAutoClose(4000);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusyState(false);
                }
            }
        }

        private static Button CreatePrimaryActionButton(string text)
        {
            var button = CreateBaseActionButton(text);
            var baseColor = Color.FromArgb(66, 133, 244);
            button.BackColor = baseColor;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = baseColor;
            button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(baseColor, 0.2f);
            button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(baseColor, 0.15f);
            return button;
        }

        private static Button CreateSecondaryActionButton(string text)
        {
            var button = CreateBaseActionButton(text);
            var accentColor = Color.FromArgb(66, 133, 244);
            button.BackColor = Color.White;
            button.ForeColor = accentColor;
            button.FlatAppearance.BorderColor = Color.FromArgb(205, 217, 243);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(237, 242, 253);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 235, 250);
            return button;
        }

        private static Button CreateBaseActionButton(string text)
        {
            var button = new Button
            {
                AutoSize = true,
                Text = text,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Margin = new Padding(0),
                Padding = new Padding(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.SemiBold)
            };

            button.FlatAppearance.BorderSize = 1;
            return button;
        }

        private async Task HandleRewriteStyleSelectedAsync(string styleKey)
        {
            var handler = RewriteRequested;
            if (handler is null)
                return;

            StopAutoClose();
            string displayName = styleKey;
            if (_rewriteStyleDisplayNames is not null && _rewriteStyleDisplayNames.TryGetValue(styleKey, out var friendly))
            {
                displayName = friendly;
            }

            UpdateMessage($"Rewriting selection ({displayName})…");
            SetBusyState(true);

            try
            {
                await handler(this, styleKey);
            }
            catch (Exception ex)
            {
                UpdateMessage($"Failed to rewrite: {ex.Message}");
                RestartAutoClose(4000);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusyState(false);
                }
            }
        }
    }
}
