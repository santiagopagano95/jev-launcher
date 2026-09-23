using System.IO;
using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class App : Application
{
    private const string MutexName = @"Local\JevLauncher.SingleInstance";
    private const string ShowEventName = @"Local\JevLauncher.ShowSignal";
    private const string ToggleEventName = @"Local\JevLauncher.ToggleSignal";

    private PanelWindow? _panel;
    private HotKey? _hotKey;
    private Settings? _settings;
    private Updater? _updater;
    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private EventWaitHandle? _toggleSignal;
    private Thread? _signalThread;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash(e.ExceptionObject?.ToString() ?? "unknown");
        DispatcherUnhandledException += (_, e) => LogCrash(e.Exception.ToString());
    }

    private static void LogCrash(string text)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Artifacts.Directory);
            File.WriteAllText(Path.Combine(Artifacts.Directory, "jevlauncher-crash.txt"), text);
        }
        catch
        {
            // nothing else we can do
        }
    }

    private static readonly bool DebugToggle = Environment.GetEnvironmentVariable("JEV_LAUNCHER_DEBUG") == "1";

    internal static void DebugLog(string text)
    {
        if (!DebugToggle) return;
        try
        {
            System.IO.Directory.CreateDirectory(Artifacts.Directory);
            File.AppendAllText(Path.Combine(Artifacts.Directory, "jev-toggle.log"),
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

        var setKeyAt = Array.IndexOf(e.Args, "--set-key");
        if (setKeyAt >= 0 && setKeyAt + 1 < e.Args.Length)
        {
            var stored = Settings.Load();
            stored.SetApiKey(e.Args[setKeyAt + 1]);
            stored.Save();
            Shutdown();
            return;
        }

        if (!smoke && !ClaimSingleInstance(wantToggle))
        {
            Shutdown();
            return;
        }

        _settings = Settings.Load();
        Autostart.Set(_settings.StartWithWindows);
        _panel = new PanelWindow(_settings);
        _panel.OpenSettingsAction = () => _panel.OpenSettings();

        if (smoke)
        {
            Dispatcher.InvokeAsync(async () =>
            {
                var report = await _panel.RunSmokeAsync();
                var tray = TrayIcon.Attach(_panel!, static () => { }, static () => { }, static () => { }, static () => { });
                report += $"tray icon => IsCreated={tray.IsCreated}{Environment.NewLine}";
                tray.Dispose();
                Artifacts.Ensure();
                File.WriteAllText(Path.Combine(Artifacts.Directory, "jev-smoke.txt"), report);
                _panel.PrepareForExit();
                Shutdown();
            });
            return;
        }

        _panel.CreateTrayIcon();

        _updater = new Updater(_settings, _panel.Notify);
        _panel.CheckUpdatesAction = () => _ = _updater.CheckManuallyAsync();
        _updater.StartAutoCheck();

        if (!suppressHotKey)
        {
            RegisterHotKey();
            _panel.SettingsChanged += RegisterHotKey;
        }

        if (wantToggle)
            Dispatcher.InvokeAsync(() => _panel.ShowPanel());
    }

    /// <summary>
    /// Returns true when this process is the launcher instance. When another instance is
    /// already running, asks it to show (or toggle) the panel and returns false.
    /// </summary>
    private bool ClaimSingleInstance(bool wantToggle)
    {
        _singleInstance = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            Signal(wantToggle ? ToggleEventName : ShowEventName);
            return false;
        }

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _toggleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName);
        _signalThread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _showSignal, _toggleSignal };
            while (true)
            {
                int index;
                try { index = WaitHandle.WaitAny(handles); }
                catch { break; }

                DebugLog(index == 0 ? "show signal received" : "toggle signal received");
                try
                {
                    if (index == 0) Dispatcher.Invoke(() => _panel?.ShowPanel());
                    else Dispatcher.Invoke(() => _panel?.Toggle());
                }
                catch
                {
                    // shutting down
                }
            }
        })
        { IsBackground = true };
        _signalThread.Start();
        return true;
    }

    private static void Signal(string eventName)
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(eventName, out var handle))
            {
                handle.Set();
                handle.Dispose();
            }
        }
        catch
        {
            // If signalling fails there is nothing useful we can do.
        }
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
        _showSignal?.Dispose();
        _toggleSignal?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
