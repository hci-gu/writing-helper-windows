using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class PopupForm : Form
    {
        private readonly Timer _timer;
        private readonly Label _label;

        public PopupForm(string text, int autohideMs)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(250, 250, 250);
            Opacity = 0.98;
            Padding = new Padding(12);

            _label = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(450, 0), // wrap long lines
                Text = text
            };

            Controls.Add(_label);
            _label.Dock = DockStyle.Fill;

            // Subtle border
            Paint += (s, e) =>
            {
                using var pen = new Pen(Color.LightGray);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };

            _timer = new Timer { Interval = autohideMs };
            _timer.Tick += (s, e) => Close();
            _timer.Start();

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
                return cp;
            }
        }
    }
}
