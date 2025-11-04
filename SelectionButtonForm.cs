using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class SelectionButtonForm : Form
    {
        private readonly Button _actionButton;
        private readonly System.Windows.Forms.Timer _autoHideTimer;

        public SelectionButtonForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.Transparent;

            _actionButton = new Button
            {
                AutoSize = true,
                Text = "Summarize",
                FlatStyle = FlatStyle.System,
                Padding = new Padding(8, 4, 8, 4)
            };
            Controls.Add(_actionButton);

            // Placeholder handler until functionality is implemented.
            _actionButton.Click += (s, e) =>
            {
                Hide();
            };

            _autoHideTimer = new System.Windows.Forms.Timer { Interval = 4000 };
            _autoHideTimer.Tick += (s, e) => Hide();
        }

        public void ShowNear(Point screenPoint)
        {
            var location = new Point(screenPoint.X, screenPoint.Y + 8);
            var preferredSize = GetPreferredSize(Size.Empty);
            var workingArea = Screen.FromPoint(location).WorkingArea;

            var x = Math.Max(workingArea.Left + 4, Math.Min(location.X, workingArea.Right - preferredSize.Width - 4));
            var y = Math.Max(workingArea.Top + 4, Math.Min(location.Y, workingArea.Bottom - preferredSize.Height - 4));

            Location = new Point(x, y);

            if (!Visible)
            {
                Show();
            }

            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible)
            {
                _autoHideTimer.Stop();
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }
    }
}
