using System;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            using var activeWindowMonitor = new ActiveWindowMonitor();
            activeWindowMonitor.Start();

            Application.ApplicationExit += (_, __) => activeWindowMonitor.Stop();

            Application.Run(new MainForm());
        }
    }
}
