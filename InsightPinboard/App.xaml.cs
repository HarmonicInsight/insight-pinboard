using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace InsightPinboard;

public partial class App : Application
{
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InsightPinboard",
        "logs");

    private static readonly string LogFile = Path.Combine(LogFolder, "app.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            LogException(args.ExceptionObject as Exception ?? new Exception("Unknown error"), "AppDomain");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            LogException(args.Exception, "Dispatcher");
            ShowErrorMessage(args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogException(args.Exception, "TaskScheduler");
            args.SetObserved();
        };
    }

    private static void LogException(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ({source}) {exception}\n";
            File.AppendAllText(LogFile, entry);
        }
        catch
        {
            // Ignore logging failures to avoid recursive crashes.
        }
    }

    private static void ShowErrorMessage(Exception exception)
    {
        try
        {
            var message = $"エラーが発生しました。\n\n{exception.Message}\n\nログ: {LogFile}";
            MessageBox.Show(message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Ignore UI errors to avoid recursive crashes.
        }
    }
}
