using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly SelectionButtonForm _selectionButton;
        private readonly HiddenMessageWindow _messageWindow;
        private readonly SynchronizationContext _syncContext;

        private readonly WinEventDelegate _textSelectionCallback;
        private IntPtr _textSelectionHook = IntPtr.Zero;

        private bool _disposed;

        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private const uint EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        public TrayApplicationContext()
        {
            _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            _selectionButton = new SelectionButtonForm();

            _menu = new ContextMenuStrip();
            _menu.Items.Add("Exit", null, (_, _) => ExitThread());

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "GlobalTextHelper",
                ContextMenuStrip = _menu
            };

            _messageWindow = new HiddenMessageWindow(OnClipboardUpdated);

            _textSelectionCallback = OnWinEvent;
            _textSelectionHook = SetWinEventHook(
                EVENT_OBJECT_TEXTSELECTIONCHANGED,
                EVENT_OBJECT_TEXTSELECTIONCHANGED,
                IntPtr.Zero,
                _textSelectionCallback,
                0,
                0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            if (_textSelectionHook == IntPtr.Zero)
            {
                Debug.WriteLine("Failed to install text selection hook. Selection button will be disabled.");
            }
        }

        protected override void ExitThreadCore()
        {
            if (_disposed)
            {
                base.ExitThreadCore();
                return;
            }

            _disposed = true;

            if (_textSelectionHook != IntPtr.Zero)
            {
                UnhookWinEvent(_textSelectionHook);
                _textSelectionHook = IntPtr.Zero;
            }

            _messageWindow.Dispose();
            _selectionButton.Dispose();

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }

            _menu.Dispose();

            base.ExitThreadCore();
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
                Debug.WriteLine("Clipboard read error: " + ex.Message);
            }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != EVENT_OBJECT_TEXTSELECTIONCHANGED)
            {
                return;
            }

            _syncContext.Post(_ =>
            {
                var anchor = GetSelectionAnchor();
                if (anchor.HasValue)
                {
                    _selectionButton.ShowNear(anchor.Value);
                }
            }, null);
        }

        private void ShowPopup(string text, int autohideMs = 3000)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var popup = new PopupForm(text, autohideMs)
            {
                StartPosition = FormStartPosition.Manual
            };

            var cursor = Cursor.Position;
            int x = cursor.X + 12;
            int y = cursor.Y + 12;

            popup.Location = new Point(x, y);
            popup.Show();

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

        #region P/Invoke

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
            int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

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

        #endregion

        private sealed class HiddenMessageWindow : NativeWindow, IDisposable
        {
            private readonly Action _clipboardUpdated;
            private bool _listening;

            public HiddenMessageWindow(Action clipboardUpdated)
            {
                _clipboardUpdated = clipboardUpdated ?? throw new ArgumentNullException(nameof(clipboardUpdated));

                CreateHandle(new CreateParams());

                _listening = AddClipboardFormatListener(Handle);
                if (!_listening)
                {
                    Debug.WriteLine("Failed to register clipboard listener. Clipboard monitoring disabled.");
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_CLIPBOARDUPDATE)
                {
                    _clipboardUpdated();
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    if (_listening)
                    {
                        RemoveClipboardFormatListener(Handle);
                        _listening = false;
                    }

                    DestroyHandle();
                }
            }
        }
    }
}
