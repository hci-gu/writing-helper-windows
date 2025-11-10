using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace GlobalTextHelper
{
    public sealed class ActiveWindowMonitor : IDisposable
    {
        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        private WinEventDelegate? _winEventProc;
        private IntPtr _hookHandle = IntPtr.Zero;
        private string? _lastWindowDescription;
        private bool _disposed;

        public void Start()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                return;
            }

            _winEventProc = WinEventProc;
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);

            if (_hookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to set active window hook.");
            }

            LogActiveWindow();
        }

        public void Stop()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            _winEventProc = null;
        }

        private void WinEventProc(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            if (eventType != EVENT_SYSTEM_FOREGROUND)
            {
                return;
            }

            LogActiveWindow(hwnd);
        }

        private void LogActiveWindow()
        {
            LogActiveWindow(GetForegroundWindow());
        }

        private void LogActiveWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            string processName = GetProcessName(hwnd);
            string title = GetWindowTitle(hwnd);
            string description = string.IsNullOrWhiteSpace(title)
                ? processName
                : $"{processName} - {title}";

            if (string.IsNullOrWhiteSpace(description) || description == _lastWindowDescription)
            {
                return;
            }

            _lastWindowDescription = description;
            Console.WriteLine($"Active window changed: {description}");

            HandleApplicationSpecificLogging(processName);
        }

        private static void HandleApplicationSpecificLogging(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return;
            }

            if (processName.Equals("OUTLOOK", StringComparison.OrdinalIgnoreCase))
            {
                LogActiveOutlookEmail();
            }
            else if (processName.Equals("MSEDGE", StringComparison.OrdinalIgnoreCase) ||
                     processName.Equals("CHROME", StringComparison.OrdinalIgnoreCase))
            {
                // Placeholder for future browser-specific handling.
            }
        }

        private static void LogActiveOutlookEmail()
        {
            Outlook.Application? outlookApp = null;
            Outlook.Explorer? explorer = null;
            Outlook.Selection? selection = null;
            Outlook.MailItem? mailItem = null;

            try
            {
                outlookApp = new Outlook.Application();
                explorer = outlookApp.ActiveExplorer();
                selection = explorer?.Selection;

                if (selection?.Count > 0)
                {
                    mailItem = selection[1] as Outlook.MailItem;
                }

                if (mailItem != null)
                {
                    string subject = mailItem.Subject ?? string.Empty;
                    Console.WriteLine($"Active email in Outlook: {subject}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to retrieve Outlook active email: {ex.Message}");
            }
            finally
            {
                if (mailItem != null)
                {
                    Marshal.ReleaseComObject(mailItem);
                }

                if (selection != null)
                {
                    Marshal.ReleaseComObject(selection);
                }

                if (explorer != null)
                {
                    Marshal.ReleaseComObject(explorer);
                }

                if (outlookApp != null)
                {
                    Marshal.ReleaseComObject(outlookApp);
                }
            }
        }

        private static string GetProcessName(IntPtr hwnd)
        {
            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);

            if (processId == 0)
            {
                return string.Empty;
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            const int bufferSize = 512;
            var buffer = new StringBuilder(bufferSize);

            return GetWindowText(hwnd, buffer, buffer.Capacity) > 0
                ? buffer.ToString()
                : string.Empty;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ActiveWindowMonitor()
        {
            Stop();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
