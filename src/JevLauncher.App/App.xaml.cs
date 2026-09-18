using System.IO;
using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class App : Application
{
    private PanelWindow? _panel;
    private HotKey? _hotKey;
    private Settings? _settings;

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

        _settings = Settings.Load();
        _panel = new PanelWindow(_settings);
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

        RegisterHotKey();
        _panel.SettingsChanged += RegisterHotKey;
    }

    private void RegisterHotKey()
    {
        var panel = _panel!;
        _hotKey?.Dispose();
        _hotKey = new HotKey(panel, _settings!.HotKey, () => panel.Toggle());
        if (!_hotKey.IsRegistered)
            MessageBox.Show(
                "The configured hotkey is already in use. Change it in Settings.",
                "Jev Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotKey?.Dispose();
        base.OnExit(e);
    }
}
