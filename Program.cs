using System;
using System.Windows.Forms;

namespace GlobalTextHelper
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            AppLogger.Initialize();

            var logPath = AppLogger.LogFilePath;
            if (!string.IsNullOrEmpty(logPath))
            {
                AppLogger.Log($"Application starting. Log file: {logPath}");
            }
            else
            {
                AppLogger.Log("Application starting. Log file unavailable; using console/debug output only.");
            }

            Application.ThreadException += (_, args) => AppLogger.LogError("Unhandled UI thread exception", args.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var exception = args.ExceptionObject as Exception ?? new Exception($"Unknown exception object: {args.ExceptionObject}");
                AppLogger.LogError("Unhandled domain exception", exception);
            };
            Application.ApplicationExit += (_, _) =>
            {
                AppLogger.Log("Application exiting.");
                AppLogger.Shutdown();
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
