using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class PopupForm : Form
    {
        private readonly Timer _timer;
        private readonly Label _label;
        private readonly Button? _simplifyButton;

        public event Func<PopupForm, Task>? SimplifyRequested;

        public PopupForm(string text, int autohideMs, bool showSimplifyButton = false)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(250, 250, 250);
            Opacity = 0.98;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = showSimplifyButton ? 2 : 1,
            };

            _label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(450, 0), // wrap long lines
                Text = text
            };

            layout.Controls.Add(_label, 0, 0);

            if (showSimplifyButton)
            {
                _simplifyButton = new Button
                {
                    AutoSize = true,
                    Text = "Simplify & Replace"
                };

                _simplifyButton.Click += async (s, e) => await HandleSimplifyClickAsync();
                layout.Controls.Add(_simplifyButton, 0, 1);
            }

            Controls.Add(layout);

            // Subtle border
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.LightGray);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            _timer = new Timer { Interval = autohideMs };
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
    }
}
