using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Whub.Core;

namespace Whub;

public partial class App : Application
{
    Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(true, "whub_single_instance", out var first);
        if (!first)
        {
            FocusRunningCopy();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandled;
        HubData.Load();
        RobloxClient.HoldMultiInstance();
        RobloxClient.ReleaseClientStorage();
        RobloxWindows.TrackPhysicalKeys();
        MainWindow = new MainWindow();
        MainWindow.Show();
        _ = AnimateUpdates.Check();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RobloxClient.ReleaseClientStorage();
        base.OnExit(e);
    }

    static void FocusRunningCopy()
    {
        var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            if (other.Id != current.Id && other.MainWindowHandle != 0)
            {
                ShowWindow(other.MainWindowHandle, 9);
                SetForegroundWindow(other.MainWindowHandle);
            }
        }
    }

    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        File.AppendAllText(Path.Combine(Paths.Root, "errors.log"), $"{DateTime.Now:s} {e.Exception}\n\n");
        MessageBox.Show(e.Exception.Message, "welffi hub", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    [DllImport("user32")] static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32")] static extern bool SetForegroundWindow(nint hwnd);
}
