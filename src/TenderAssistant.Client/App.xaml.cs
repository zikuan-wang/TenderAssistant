using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using TenderAssistant.Client.Services;

namespace TenderAssistant.Client;

public partial class App : Application
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant",
        "client-crash.log");

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppThemeService.Initialize();
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog("CurrentDomainUnhandledException", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("UnobservedTaskException", e.Exception);
    }

    private static void WriteCrashLog(string source, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}");
            builder.AppendLine(exception?.ToString() ?? "No exception information.");
            builder.AppendLine(new string('-', 80));

            File.AppendAllText(CrashLogPath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Ignore secondary logging failures.
        }
    }
}
