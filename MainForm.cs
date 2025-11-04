using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class MainForm : Form
    {
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private readonly SelectionButtonForm _selectionButton;

        // Clipboard listener
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        // WinEvent hook (text selection changed)
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private const uint EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private IntPtr _hTextSelHook = IntPtr.Zero;
        private WinEventDelegate _textSelCallback;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        public MainForm()
        {
            Text = "GlobalTextHelper";
            // Keep an invisible background window to receive Windows messages
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-2000, -2000);      // put it off-screen
            FormBorderStyle = FormBorderStyle.FixedToolWindow;

            // Tray icon + menu
            _menu = new ContextMenuStrip();
            _menu.Items.Add("Exit", null, (s, e) => Application.Exit());

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "GlobalTextHelper",
                ContextMenuStrip = _menu
            };

            _selectionButton = new SelectionButtonForm();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _selectionButton?.Dispose();

                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                }

                _menu?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Start listening for clipboard changes
            AddClipboardFormatListener(Handle);

            // Hook global text-selection events (out-of-process, no injection)
            _textSelCallback = OnWinEvent;
            _hTextSelHook = SetWinEventHook(
                EVENT_OBJECT_TEXTSELECTIONCHANGED, EVENT_OBJECT_TEXTSELECTIONCHANGED,
                IntPtr.Zero, _textSelCallback, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            RemoveClipboardFormatListener(Handle);

            if (_hTextSelHook != IntPtr.Zero)
            {
                UnhookWinEvent(_hTextSelHook);
                _hTextSelHook = IntPtr.Zero;
            }

            base.OnHandleDestroyed(e);
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!IsHandleCreated)
            {
                CreateHandle();
            }

            // Keep the window hidden while retaining a handle for message pumping.
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdated();
            }
            base.WndProc(ref m);
        }

        private void OnClipboardUpdated()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string summary = Summarizer.Summarize(text, 200);
                        ShowPopup(summary);
                    }
                }
            }
            catch (Exception ex)
            {
                // Clipboard can be momentarily busy or not text; ignore quietly.
                System.Diagnostics.Debug.WriteLine("Clipboard read error: " + ex.Message);
            }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != EVENT_OBJECT_TEXTSELECTIONCHANGED)
                return;

            // Marshal to UI thread to interact with our forms
            BeginInvoke(new Action(() =>
            {
                var anchor = GetSelectionAnchor();
                if (anchor.HasValue)
                {
                    _selectionButton.ShowNear(anchor.Value);
                }
            }));
        }

        private void ShowPopup(string text, int autohideMs = 3000)
        {
            var popup = new PopupForm(text, autohideMs);
            var cursor = Cursor.Position;

            int x = cursor.X + 12;
            int y = cursor.Y + 12;

            popup.StartPosition = FormStartPosition.Manual;
            popup.Location = new Point(x, y);
            popup.Show();

            // Keep the popup on-screen if near edges
            popup.BeginInvoke(new Action(() =>
            {
                var screen = Screen.FromPoint(cursor).WorkingArea;
                var size = popup.Size;
                int nx = Math.Min(x, screen.Right - size.Width - 8);
                int ny = Math.Min(y, screen.Bottom - size.Height - 8);
                nx = Math.Max(screen.Left + 8, nx);
                ny = Math.Max(screen.Top + 8, ny);
                popup.Location = new Point(nx, ny);
            }));
        }

        private Point? GetSelectionAnchor()
        {
            var guiInfo = new GUITHREADINFO
            {
                cbSize = Marshal.SizeOf<GUITHREADINFO>()
            };

            if (GetGUIThreadInfo(0, ref guiInfo) && guiInfo.hwndCaret != IntPtr.Zero)
            {
                var caretPoint = new POINT
                {
                    X = guiInfo.rcCaret.Left,
                    Y = guiInfo.rcCaret.Bottom
                };

                if (ClientToScreen(guiInfo.hwndCaret, ref caretPoint))
                {
                    return new Point(caretPoint.X, caretPoint.Y);
                }
            }

            if (GetCursorPos(out var cursorPoint))
            {
                return new Point(cursorPoint.X, cursorPoint.Y);
            }

            return null;
        }
    }
}
