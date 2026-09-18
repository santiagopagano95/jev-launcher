using System.IO;
using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class App : Application
{
    private PanelWindow? _panel;
    private HotKey? _hotKey;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject?.ToString() ?? "unknown");
        DispatcherUnhandledException += (_, e) => LogCrash(e.Exception.ToString());
    }

    private static void LogCrash(string text)
    {
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "jevlauncher-crash.txt"), text);
        }
        catch
        {
            // nothing else we can do
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _panel = new PanelWindow();
        _panel.OpenSettingsAction = () => _panel.OpenSettings();

        if (e.Args.Contains("--smoke"))
        {
            Dispatcher.InvokeAsync(async () =>
            {
                var report = await _panel.RunSmokeAsync();
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "jev-smoke.txt"), report);
                Shutdown();
            });
            return;
        }

        _panel.CreateTrayIcon();

        _hotKey = new HotKey(_panel, () => _panel.Toggle());
        if (!_hotKey.IsRegistered)
            MessageBox.Show(
                "Alt+Space is already in use. Use the tray icon to toggle the launcher.",
                "Jev Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKey?.Dispose();
        base.OnExit(e);
    }
}
