using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    internal class SelectionOverlayForm : Form
    {
        private readonly Button _highlightButton;
        private readonly Button _copyButton;

        public event EventHandler? HighlightRequested;
        public event EventHandler? CopyRequested;

        public SelectionOverlayForm()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Padding = new Padding(0);
            BackColor = Color.Transparent;

            var container = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(8),
                Margin = new Padding(0),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layout = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };

            _highlightButton = CreateActionButton("Highlight");
            _copyButton = CreateActionButton("Copy");

            _highlightButton.Click += (s, e) => HighlightRequested?.Invoke(this, EventArgs.Empty);
            _copyButton.Click += (s, e) => CopyRequested?.Invoke(this, EventArgs.Empty);

            layout.Controls.Add(_highlightButton);
            layout.Controls.Add(_copyButton);
            container.Controls.Add(layout);
            Controls.Add(container);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        private static Button CreateActionButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(4, 0, 4, 0),
                Padding = new Padding(8, 4, 8, 4),
                FlatStyle = FlatStyle.System
            };
        }
    }
}
