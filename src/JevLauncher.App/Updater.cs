using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using JevLauncher.Core;

namespace JevLauncher.App;

public sealed class Updater
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly Settings _settings;
    private readonly Action<string> _notify;
    private readonly HttpClient _http = new();
    private readonly UpdateChecker _checker;
    private readonly UpdateDownloader _downloader;
    private readonly DispatcherTimer _timer;
    private bool _busy;

    public Updater(Settings settings, Action<string> notify)
    {
        _settings = settings;
        _notify = notify;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _checker = new UpdateChecker(_http);
        _downloader = new UpdateDownloader(_http);
        _timer = new DispatcherTimer { Interval = InitialDelay };
        _timer.Tick += async (_, _) =>
        {
            _timer.Stop();
            _timer.Interval = Interval;
            _timer.Start();
            await CheckAndPromptAsync();
        };
    }

    public static Version CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);

    public void StartAutoCheck()
    {
        if (!_settings.CheckForUpdates) return;
        _timer.Start();
    }

    public async Task CheckManuallyAsync()
    {
        var result = await _checker.CheckAsync(CurrentVersion);
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable when result.Release is not null:
                PromptAndApply(result.Release);
                break;
            case UpdateCheckStatus.UpToDate:
                _notify("Ya estás al día.");
                break;
            default:
                _notify("No se pudo consultar actualizaciones.");
                break;
        }
    }

    private async Task CheckAndPromptAsync()
    {
        if (_busy) return;
        var result = await _checker.CheckAsync(CurrentVersion);
        if (result.Status == UpdateCheckStatus.UpdateAvailable && result.Release is not null)
            PromptAndApply(result.Release);
    }

    private void PromptAndApply(ReleaseInfo release)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var dir = Path.Combine(AppData.Directory, "updates");
            var window = new UpdateWindow(release, _downloader, dir);
            window.ShowDialog();
            if (window.Accepted && window.SetupPath is not null)
                Apply(window.SetupPath);
        }
        catch (Exception ex)
        {
            App.DebugLog("update failed: " + ex.Message);
        }
        finally
        {
            _busy = false;
        }
    }

    private static void Apply(string setupPath)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "JevLauncher.App.exe");
        var arguments = $"/c \"\"{setupPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART && start \"\" \"{exe}\"\"";
        Process.Start(new ProcessStartInfo("cmd.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        Application.Current.Shutdown();
    }
}
