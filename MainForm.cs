using System;
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
        private readonly MessageLoopForm _messageLoopForm;
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
            AppLogger.Log("Initializing tray application context.");

            _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            _selectionButton = new SelectionButtonForm();

            _messageLoopForm = new MessageLoopForm();
            MainForm = _messageLoopForm;
            _messageLoopForm.CreateControl();
            AppLogger.Log("Hidden message loop form created.");

            _menu = new ContextMenuStrip();
            _menu.Items.Add("Exit", null, (_, _) => ExitThread());

            _tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "GlobalTextHelper",
                ContextMenuStrip = _menu
            };
            AppLogger.Log("Tray icon created and made visible.");

            _messageWindow = new HiddenMessageWindow(OnClipboardUpdated);
            AppLogger.Log("Hidden message window created for clipboard monitoring.");

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
                AppLogger.Log("Failed to install text selection hook. Selection button will be disabled.");
            }
            else
            {
                AppLogger.Log($"Text selection hook installed: 0x{_textSelectionHook.ToInt64():X}");
            }
        }

        protected override void ExitThreadCore()
        {
            AppLogger.Log("ExitThreadCore invoked.");

            if (_disposed)
            {
                AppLogger.Log("Application context already disposed; delegating to base implementation.");
                base.ExitThreadCore();
                return;
            }

            _disposed = true;
            AppLogger.Log("Disposing tray application context resources.");

            if (_textSelectionHook != IntPtr.Zero)
            {
                UnhookWinEvent(_textSelectionHook);
                AppLogger.Log("Text selection hook removed.");
                _textSelectionHook = IntPtr.Zero;
            }

            _messageWindow.Dispose();
            AppLogger.Log("Hidden message window disposed.");
            _selectionButton.Dispose();
            AppLogger.Log("Selection button disposed.");

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                AppLogger.Log("Tray icon disposed.");
            }

            _menu.Dispose();
            AppLogger.Log("Tray menu disposed.");

            if (_messageLoopForm != null)
            {
                if (!_messageLoopForm.IsDisposed)
                {
                    _messageLoopForm.ForceClose();
                }
                _messageLoopForm.Dispose();
                AppLogger.Log("Hidden message loop form disposed.");
            }

            base.ExitThreadCore();
        }

        private void OnClipboardUpdated()
        {
            try
            {
                AppLogger.Log("Clipboard update detected.");
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        AppLogger.Log($"Clipboard text length: {text.Length}");
                        string summary = Summarizer.Summarize(text, 200);
                        AppLogger.Log($"Summary generated with length {summary.Length}.");
                        ShowPopup(summary);
                    }
                    else
                    {
                        AppLogger.Log("Clipboard text empty or whitespace; ignoring.");
                    }
                }
                else
                {
                    AppLogger.Log("Clipboard does not contain text; ignoring.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Clipboard read error", ex);
            }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != EVENT_OBJECT_TEXTSELECTIONCHANGED)
            {
                AppLogger.Log($"Received unexpected WinEvent: 0x{eventType:X}");
                return;
            }

            _syncContext.Post(_ =>
            {
                var anchor = GetSelectionAnchor();
                if (anchor.HasValue)
                {
                    AppLogger.Log($"Showing selection button near {anchor.Value}.");
                    _selectionButton.ShowNear(anchor.Value);
                }
                else
                {
                    AppLogger.Log("Selection anchor not found; hiding selection button.");
                    _selectionButton.Hide();
                }
            }, null);
        }

        private void ShowPopup(string text, int autohideMs = 3000)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                AppLogger.Log("Popup requested with empty text; skipping display.");
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
            AppLogger.Log($"Popup shown at initial location ({x}, {y}).");

            popup.BeginInvoke(new Action(() =>
            {
                var screen = Screen.FromPoint(cursor).WorkingArea;
                var size = popup.Size;

                int nx = Math.Min(x, screen.Right - size.Width - 8);
                int ny = Math.Min(y, screen.Bottom - size.Height - 8);
                nx = Math.Max(screen.Left + 8, nx);
                ny = Math.Max(screen.Top + 8, ny);

                popup.Location = new Point(nx, ny);
                AppLogger.Log($"Popup repositioned to ({nx}, {ny}).");
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
                    AppLogger.Log($"Caret anchor at ({caretPoint.X}, {caretPoint.Y}).");
                    return new Point(caretPoint.X, caretPoint.Y);
                }
                else
                {
                    AppLogger.Log("ClientToScreen failed for caret position.");
                }
            }

            if (GetCursorPos(out var cursorPoint))
            {
                AppLogger.Log($"Using cursor position ({cursorPoint.X}, {cursorPoint.Y}) as anchor.");
                return new Point(cursorPoint.X, cursorPoint.Y);
            }

            AppLogger.Log("Unable to determine selection anchor.");
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
                    AppLogger.Log("Failed to register clipboard listener. Clipboard monitoring disabled.");
                }
                else
                {
                    AppLogger.Log("Clipboard listener registered successfully.");
                }
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_CLIPBOARDUPDATE)
                {
                    AppLogger.Log("WM_CLIPBOARDUPDATE received.");
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
                        AppLogger.Log("Clipboard listener removed.");
                    }

                    DestroyHandle();
                    AppLogger.Log("Hidden message window handle destroyed.");
                }
            }
        }

        private sealed class MessageLoopForm : Form
        {
            private bool _allowClose;

            public MessageLoopForm()
            {
                ShowInTaskbar = false;
                Opacity = 0;
                Size = new Size(0, 0);
                FormBorderStyle = FormBorderStyle.FixedToolWindow;
                StartPosition = FormStartPosition.Manual;
                Location = new Point(-32000, -32000);
            }

            protected override void SetVisibleCore(bool value)
            {
                base.SetVisibleCore(false);
            }

            protected override bool ShowWithoutActivation => true;

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (!_allowClose)
                {
                    e.Cancel = true;
                }

                base.OnFormClosing(e);
            }

            public void ForceClose()
            {
                _allowClose = true;
                Close();
            }
        }
    }
}
