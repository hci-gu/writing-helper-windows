using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class MainForm : Form
    {
        private const int OverlayMargin = 8;

        private readonly RichTextBox _editor;
        private readonly SelectionOverlayForm _overlayForm;
        private Rectangle? _currentSelectionBoundsScreen;

        public MainForm()
        {
            Text = "Writing Helper";
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;

            _editor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                AcceptsTab = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(32, 32, 32)
            };

            Controls.Add(_editor);

            _overlayForm = new SelectionOverlayForm();
            _overlayForm.HighlightRequested += OnHighlightRequested;
            _overlayForm.CopyRequested += OnCopyRequested;

            _editor.SelectionChanged += OnEditorSelectionChanged;
            _editor.VScroll += OnEditorScrolled;
            _editor.HScroll += OnEditorScrolled;
            _editor.TextChanged += OnEditorTextChanged;
            _editor.Resize += OnEditorLayoutChanged;

            Move += OnHostMovedOrResized;
            Resize += OnHostMovedOrResized;
            ClientSizeChanged += OnHostMovedOrResized;
        }

        internal RichTextBox Editor => _editor;
        internal bool SelectionOverlayVisible => _overlayForm.Visible;
        internal SelectionOverlayForm OverlayForm => _overlayForm;
        internal Rectangle? CurrentSelectionBounds => _currentSelectionBoundsScreen;

        private void OnEditorSelectionChanged(object? sender, EventArgs e)
        {
            UpdateOverlayVisibility();
        }

        private void OnEditorScrolled(object? sender, EventArgs e)
        {
            if (_overlayForm.Visible)
            {
                UpdateOverlayPosition();
            }
        }

        private void OnEditorTextChanged(object? sender, EventArgs e)
        {
            if (_overlayForm.Visible)
            {
                UpdateOverlayPosition();
            }
        }

        private void OnEditorLayoutChanged(object? sender, EventArgs e)
        {
            if (_overlayForm.Visible)
            {
                UpdateOverlayPosition();
            }
        }

        private void OnHostMovedOrResized(object? sender, EventArgs e)
        {
            if (_overlayForm.Visible)
            {
                UpdateOverlayPosition();
            }
        }

        private void OnHighlightRequested(object? sender, EventArgs e)
        {
            string selectedText = _editor.SelectedText;
            Console.WriteLine($"Highlight button clicked – selected text: {selectedText}");
        }

        private void OnCopyRequested(object? sender, EventArgs e)
        {
            string selectedText = _editor.SelectedText;
            Console.WriteLine($"Copy button clicked – selected text: {selectedText}");
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            HideOverlay();
        }

        private void UpdateOverlayVisibility()
        {
            if (_editor.SelectionLength <= 0)
            {
                HideOverlay();
                return;
            }

            var selectionBounds = CalculateSelectionBoundsInScreen();
            if (selectionBounds is null)
            {
                HideOverlay();
                return;
            }

            _currentSelectionBoundsScreen = selectionBounds;

            if (!_overlayForm.Visible)
            {
                _overlayForm.Show(this);
            }

            PositionOverlay(selectionBounds.Value);
        }

        private void HideOverlay()
        {
            _currentSelectionBoundsScreen = null;
            if (_overlayForm.Visible)
            {
                _overlayForm.Hide();
            }
        }

        private void UpdateOverlayPosition()
        {
            var selectionBounds = CalculateSelectionBoundsInScreen();
            if (selectionBounds is null)
            {
                HideOverlay();
                return;
            }

            _currentSelectionBoundsScreen = selectionBounds;
            PositionOverlay(selectionBounds.Value);
        }

        private void PositionOverlay(Rectangle selectionBounds)
        {
            Size overlaySize = _overlayForm.Size;
            if (overlaySize.Width == 0 || overlaySize.Height == 0)
            {
                overlaySize = _overlayForm.GetPreferredSize(Size.Empty);
            }

            Rectangle workingArea = Screen.FromControl(this).WorkingArea;

            int x = selectionBounds.Left;
            int y = selectionBounds.Bottom + OverlayMargin;

            if (x + overlaySize.Width > workingArea.Right)
            {
                x = workingArea.Right - overlaySize.Width - OverlayMargin;
            }

            if (x < workingArea.Left)
            {
                x = workingArea.Left + OverlayMargin;
            }

            if (y + overlaySize.Height > workingArea.Bottom)
            {
                y = selectionBounds.Top - overlaySize.Height - OverlayMargin;
                if (y < workingArea.Top)
                {
                    y = workingArea.Top + OverlayMargin;
                }
            }

            _overlayForm.Location = new Point(x, y);
            _overlayForm.BringToFront();
        }

        private Rectangle? CalculateSelectionBoundsInScreen()
        {
            if (_editor.SelectionLength <= 0)
            {
                return null;
            }

            int start = _editor.SelectionStart;
            int length = _editor.SelectionLength;
            int end = start + length;

            Point startPoint = _editor.GetPositionFromCharIndex(start);
            Point endPoint;
            if (length == 0)
            {
                endPoint = startPoint;
            }
            else if (end < _editor.TextLength)
            {
                endPoint = _editor.GetPositionFromCharIndex(end);
            }
            else
            {
                endPoint = _editor.GetPositionFromCharIndex(Math.Max(0, end - 1));
                Size charSize = TextRenderer.MeasureText(" ", _editor.Font);
                endPoint = new Point(endPoint.X + charSize.Width, endPoint.Y);
            }

            int lineHeight = TextRenderer.MeasureText("Ag", _editor.Font).Height;

            int left = Math.Min(startPoint.X, endPoint.X);
            int right = Math.Max(startPoint.X, endPoint.X);
            int top = Math.Min(startPoint.Y, endPoint.Y);
            int bottom = Math.Max(startPoint.Y, endPoint.Y) + lineHeight;

            int width = Math.Max(1, right - left);
            int height = Math.Max(lineHeight, bottom - top);

            Rectangle clientRect = new Rectangle(left, top, width, height);
            Point screenTopLeft = _editor.PointToScreen(clientRect.Location);

            return new Rectangle(screenTopLeft, clientRect.Size);
        }

        internal void ForceOverlayRefreshForTest()
        {
            UpdateOverlayVisibility();
        }

        internal void ForceOverlayRepositionForTest()
        {
            UpdateOverlayPosition();
        }
    }
}
