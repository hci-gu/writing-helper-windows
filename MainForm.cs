using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    public class MainForm : Form
    {
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private readonly TextSelectionPromptBuilder _promptBuilder = new();
        private OpenAiChatClient? _openAiClient;
        private IntPtr _lastFocusedWindow = IntPtr.Zero;

        // Clipboard listener
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

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

        private IntPtr _hTextSelHook = IntPtr.Zero;
        private WinEventDelegate _textSelCallback;

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
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Hide(); // Run as a background tray app
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
                    _lastFocusedWindow = GetForegroundWindow();
                    string text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string summary = Summarizer.Summarize(text, 200);
                        ShowPopup(summary, autohideMs: 3000, simplifyHandler: popup => SimplifySelectionAsync(popup, text));
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
                // Barebones behavior: nudge the user that a selection occurred.
                // (You can later plug UI Automation here to read highlighted text.)
                ShowPopup("Text selection changed (press Ctrl+C to summarize).", autohideMs: 1500);
            }));
        }

        private void ShowPopup(string text, int autohideMs = 3000, Func<PopupForm, Task>? simplifyHandler = null)
        {
            bool showSimplifyButton = simplifyHandler is not null;
            var popup = new PopupForm(text, autohideMs, showSimplifyButton);
            if (simplifyHandler is not null)
            {
                popup.SimplifyRequested += simplifyHandler;
            }
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

        private async Task SimplifySelectionAsync(PopupForm popup, string originalText)
        {
            try
            {
                var client = GetOrCreateOpenAiClient();
                string simplified = await _promptBuilder.SimplifySelectionAsync(client, originalText);

                if (string.IsNullOrWhiteSpace(simplified))
                {
                    popup.UpdateMessage("The assistant returned an empty response.");
                    popup.RestartAutoClose(3000);
                    return;
                }

                ReplaceSelectionWithText(originalText, simplified);
                popup.UpdateMessage("Simplified text inserted.");
                popup.RestartAutoClose(1500);
            }
            catch (Exception ex)
            {
                popup.UpdateMessage($"Unable to simplify: {ex.Message}");
                popup.RestartAutoClose(4000);
            }
        }

        private OpenAiChatClient GetOrCreateOpenAiClient()
        {
            return _openAiClient ??= OpenAiChatClient.FromEnvironment();
        }

        private void ReplaceSelectionWithText(string originalText, string replacement)
        {
            if (string.IsNullOrEmpty(replacement))
                throw new InvalidOperationException("Replacement text cannot be empty.");

            IDataObject? clipboardData = null;
            try
            {
                clipboardData = Clipboard.GetDataObject();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Clipboard snapshot failed: " + ex.Message);
            }

            try
            {
                Clipboard.SetText(replacement);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to set clipboard text: " + ex.Message);
            }

            if (_lastFocusedWindow != IntPtr.Zero)
            {
                SetForegroundWindow(_lastFocusedWindow);
            }

            SendKeys.SendWait("^v");

            try
            {
                if (clipboardData is not null)
                {
                    Clipboard.SetDataObject(clipboardData);
                }
                else if (!string.IsNullOrEmpty(originalText))
                {
                    Clipboard.SetText(originalText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Clipboard restore failed: " + ex.Message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _openAiClient?.Dispose();
                _tray?.Dispose();
                _menu?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
