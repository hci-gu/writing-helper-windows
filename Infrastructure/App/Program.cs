using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GlobalTextHelper.Infrastructure.App;

namespace GlobalTextHelper
{
    internal static class Program
    {
#if DEBUG
        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll")] static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll")] static extern bool AllocConsole();
        [DllImport("kernel32.dll")] static extern bool FreeConsole();
#endif

        [STAThread]
        static void Main()
        {
#if DEBUG
            // Keep console attached in debug builds for diagnostics.
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                AllocConsole();
            }

            Console.OutputEncoding = System.Text.Encoding.UTF8;
#endif

            ApplicationConfiguration.Initialize();

            using var appHost = new AppHost();
#if DEBUG
            try
            {
                Application.Run(appHost);
            }
            finally
            {
                FreeConsole();
            }
#else
            Application.Run(appHost);
#endif
        }
    }
}
