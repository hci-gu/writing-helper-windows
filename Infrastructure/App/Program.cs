using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GlobalTextHelper.Infrastructure.App;

namespace GlobalTextHelper
{
    internal static class Program
    {
        private const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;

        [DllImport("kernel32.dll")] static extern bool AttachConsole(uint dwProcessId);
        [DllImport("kernel32.dll")] static extern bool AllocConsole();
        [DllImport("kernel32.dll")] static extern bool FreeConsole();

        [STAThread]
        static void Main()
        {
            // Try to attach to the parent console (e.g., the one you ran `dotnet run` from).
            if (!AttachConsole(ATTACH_PARENT_PROCESS))
            {
                // If there isn't one (e.g., started from Explorer), make a new console.
                AllocConsole();
            }

            // optional: so UTF-8 glyphs print nicely
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            ApplicationConfiguration.Initialize();

            using var appHost = new AppHost();
            try
            {
                Application.Run(appHost);
            }
            finally
            {
                FreeConsole();
            }
        }
    }
}
