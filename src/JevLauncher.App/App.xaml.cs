using System.IO;
using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class App : Application
{
    private const string MutexName = @"Local\JevLauncher.SingleInstance";
    private const string ToggleEventName = @"Local\JevLauncher.ToggleSignal";

    private PanelWindow? _panel;
    private HotKey? _hotKey;
    private Settings? _settings;
    private Mutex? _singleInstance;
    private EventWaitHandle? _toggleSignal;
    private Thread? _toggleThread;

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

    private static readonly bool DebugToggle = Environment.GetEnvironmentVariable("JEV_LAUNCHER_DEBUG") == "1";

    private static void DebugLog(string text)
    {
        if (!DebugToggle) return;
        try
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "jev-toggle.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {text}{Environment.NewLine}");
        }
        catch
        {
            // diagnostics only
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var smoke = e.Args.Contains("--smoke");
        var wantToggle = e.Args.Contains("--toggle");
        var suppressHotKey = e.Args.Contains("--no-hotkey");

        if (!smoke && !ClaimSingleInstance(wantToggle))
        {
            Shutdown();
            return;
        }

        _settings = Settings.Load();
        _panel = new PanelWindow(_settings);
        _panel.OpenSettingsAction = () => _panel.OpenSettings();

        if (smoke)
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

        if (!suppressHotKey)
        {
            RegisterHotKey();
            _panel.SettingsChanged += RegisterHotKey;
        }

        if (wantToggle)
            Dispatcher.InvokeAsync(() => _panel.ShowPanel());
    }

    /// <summary>
    /// Returns true when this process is the launcher instance. When another
    /// instance is already running, signals it to toggle the panel and returns false.
    /// </summary>
    private bool ClaimSingleInstance(bool signalToggle)
    {
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            if (signalToggle)
            {
                try
                {
                    if (EventWaitHandle.TryOpenExisting(ToggleEventName, out var signal))
                    {
                        signal.Set();
                        signal.Dispose();
                    }
                }
                catch
                {
                    // If signalling fails there is nothing useful we can do.
                }
            }
            return false;
        }

        _toggleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
        _toggleThread = new Thread(() =>
        {
            while (_toggleSignal.WaitOne())
            {
                try
                {
                    DebugLog("toggle signal received");
                    Dispatcher.Invoke(() => _panel?.Toggle());
                    DebugLog("panel toggled");
                }
                catch { /* shutting down */ }
            }
        })
        { IsBackground = true };
        _toggleThread.Start();
        return true;
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
        _toggleSignal?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
