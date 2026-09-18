using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class PanelWindow : Window
{
    private readonly Stats _stats = new();
    private readonly Settings _settings;
    private readonly JevClient _client;
    private readonly LauncherEngine _engine;
    private readonly IClipboardKindProvider _clipboard = new WindowsClipboardKindProvider();
    private readonly List<string> _recentApps = new();
    private readonly ObservableCollection<RowView> _rows = new();
    private readonly LauncherServices _services = new();
    private readonly HttpClient _braveHttp = new();
    private const int MaxRows = 7;
    private const int PaletteRows = 14;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _debounce;

    private TaskbarIcon? _tray;
    private HwndSource? _clipboardSource;
    private DispatcherTimer? _timer;
    private string _flash = string.Empty;
    private bool _suppressHide;
    private int _indexBuildInFlight;
    private volatile bool _indexReady;
    private int _selected;
    private string _lastError = string.Empty;

    public Action? OpenSettingsAction { get; set; }
    public event Action? SettingsChanged;

    public PanelWindow(Settings settings)
    {
        InitializeComponent();
        Rows.ItemsSource = _rows;

        _settings = settings;
        _client = new JevClient(new HttpClient(), _settings.GetApiKey());

        _services.Snippets = _settings.Snippets;
        _services.Notes = new NotesStore(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JevLauncher", "notes.txt"));
        _services.Windows = WindowList.Build;
        ConfigureWebSearch();

        _engine = new LauncherEngine(
            Array.Empty<Candidate>(),
            _client,
            _stats,
            () => ContextProvider.Capture(_recentApps, _clipboard),
            _services);

        _clipboardSource = HwndSource.FromHwnd(new WindowInteropHelper(this).EnsureHandle());
        _clipboardSource!.AddHook(ClipboardHook);
        AddClipboardFormatListener(_clipboardSource.Handle);

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!IsActive && IsVisible && !_suppressHide) HidePanel();
        };

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RunQueryAsync();
        };

        _ = Task.Run(BuildIndex);
    }

    private void ConfigureWebSearch()
    {
        var key = _settings.GetBraveApiKey();
        _services.WebSearch = string.IsNullOrWhiteSpace(key)
            ? null
            : new BraveWebSearch(_braveHttp, key);
    }

    public void CreateTrayIcon()
    {
        _tray = TrayIcon.Attach(
            this,
            Toggle,
            () => OpenSettingsAction?.Invoke(),
            () =>
            {
                _tray?.Dispose();
                Application.Current.Shutdown();
            });
    }

    public void Toggle()
    {
        if (IsVisible && IsActive) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
        App.DebugLog("ShowPanel");
        _lastError = string.Empty;
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height * 0.2;

        Show();
        Activate();
        SetForegroundWindow(new WindowInteropHelper(this).Handle);
        QueryBox.Focus();
        QueryBox.SelectAll();
        RenderRows(_engine.Update(QueryBox.Text ?? string.Empty));
        _ = Task.Run(BuildIndex);
        Acrylic.Apply(this, dark: true);
    }

    public void HidePanel()
    {
        App.DebugLog("HidePanel");
        _hideTimer.Stop();
        _debounce.Stop();
        Hide();
        QueryBox.Text = string.Empty;
        _rows.Clear();
        _selected = 0;
    }

    public void OpenSettings()
    {
        var window = new SettingsWindow(_settings);
        if (IsVisible) window.Owner = this;
        if (window.ShowDialog() == true)
        {
            _settings.Save();
            _client.SetApiKey(_settings.GetApiKey());
            _services.Snippets = _settings.Snippets;
            ConfigureWebSearch();
            SettingsChanged?.Invoke();
            UpdateFooter();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Acrylic.Apply(this, dark: true);

    private void OnActivated(object? sender, EventArgs e) => _hideTimer.Stop();

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_suppressHide) return;
        _hideTimer.Start();
    }

    private void OnQueryChanged(object sender, TextChangedEventArgs e)
    {
        _lastError = string.Empty;
        RenderRows(_engine.Update(QueryBox.Text ?? string.Empty));
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RunQueryAsync()
    {
        var query = QueryBox.Text ?? string.Empty;
        SetInFlight(true);
        try
        {
            var rows = await _engine.UpdateAsync(query);
            if ((QueryBox.Text ?? string.Empty) != query) return;
            RenderRows(rows);
        }
        finally
        {
            SetInFlight(false);
        }
    }

    private void SetInFlight(bool inFlight)
    {
        if (inFlight)
        {
            var pulse = new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(700))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            AccentLine.BeginAnimation(OpacityProperty, pulse);
        }
        else
        {
            AccentLine.BeginAnimation(OpacityProperty, null);
            AccentLine.Opacity = 0.3;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HidePanel();
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) CopySecondary();
                else ExecuteSelected();
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (_rows.Count == 0) return;
        var next = ((_selected + delta) % _rows.Count + _rows.Count) % _rows.Count;
        _rows[_selected].IsSelected = false;
        _selected = next;
        _rows[_selected].IsSelected = true;
        ScrollSelectionIntoView();
    }

    private void ScrollSelectionIntoView()
    {
        if (_selected < 0 || _selected >= _rows.Count) return;
        if (Rows.ItemContainerGenerator.ContainerFromIndex(_selected) is not FrameworkElement container) return;

        var position = container.TransformToAncestor(RowsScroll).Transform(new Point(0, 0));
        if (position.Y < 0)
            RowsScroll.ScrollToVerticalOffset(RowsScroll.VerticalOffset + position.Y);
        else if (position.Y + container.ActualHeight > RowsScroll.ViewportHeight)
            RowsScroll.ScrollToVerticalOffset(
                RowsScroll.VerticalOffset + position.Y + container.ActualHeight - RowsScroll.ViewportHeight);
    }

    private void ExecuteSelected()
    {
        if (_selected < 0 || _selected >= _rows.Count) return;
        var candidate = _rows[_selected].Candidate;

        // Command palette rows insert their text; app actions run immediately.
        if (candidate.Id.StartsWith("app:", StringComparison.Ordinal))
        {
            RunAppAction(candidate.Id);
            return;
        }

        if (candidate.Kind == CandidateKind.Command)
        {
            QueryBox.Text = candidate.Target ?? string.Empty;
            QueryBox.CaretIndex = QueryBox.Text.Length;
            return;
        }

        if (candidate.Kind == CandidateKind.SaveNote)
        {
            _services.Notes?.Append(candidate.Target ?? string.Empty);
            _flash = "Note saved";
            HidePanel();
            return;
        }

        if (candidate.Kind == CandidateKind.Timer)
        {
            StartTimer(long.TryParse(candidate.Target, out var ms) ? ms : 0);
            _flash = "Timer set";
            HidePanel();
            return;
        }

        _suppressHide = true;
        try
        {
            if (candidate.Kind == CandidateKind.SystemToggle)
            {
                SystemToggles.Execute(candidate.Target ?? candidate.Id, msg => _lastError = msg);
            }
            else
            {
                Executor.Launch(candidate, SetClipboard, _settings.SearchTemplate);
            }

            if (candidate.Kind == CandidateKind.OpenApp) AddRecentApp(candidate.Title);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }
        finally
        {
            _suppressHide = false;
        }

        if (string.IsNullOrEmpty(_lastError)) HidePanel();
        else UpdateFooter();
    }

    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private IntPtr ClipboardHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            try
            {
                if (Clipboard.ContainsText()) _services.Clipboard.Push(Clipboard.GetText());
            }
            catch
            {
                // The clipboard can be locked by another process.
            }
        }
        return IntPtr.Zero;
    }

    private void CopySecondary()
    {
        if (_selected < 0 || _selected >= _rows.Count) return;
        var target = _rows[_selected].Candidate.Target;
        if (string.IsNullOrWhiteSpace(target)) return;

        SetClipboard(target);
        Flash("Copied to clipboard");
    }

    private void StartTimer(long milliseconds)
    {
        if (milliseconds <= 0) return;

        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        _timer.Tick += (_, _) =>
        {
            _timer!.Stop();
            Notify("Timer finished");
        };
        _timer.Start();
    }

    private void Notify(string message)
    {
        try
        {
            if (_tray is not null) _tray.TrayIcon.ShowNotification("Jev Launcher", message);
            else MessageBox.Show(message, "Jev Launcher");
        }
        catch
        {
            // Notifications are best-effort.
        }
    }

    private void Flash(string message)
    {
        _flash = message;
        UpdateFooter();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _flash = string.Empty;
            UpdateFooter();
        };
        timer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_clipboardSource is not null)
        {
            RemoveClipboardFormatListener(_clipboardSource.Handle);
            _clipboardSource.RemoveHook(ClipboardHook);
        }
        base.OnClosed(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private void RunAppAction(string id)    {
        switch (id)
        {
            case "app:settings":
                OpenSettingsAction?.Invoke();
                HidePanel();
                break;
            case "app:quit":
                Application.Current.Shutdown();
                break;
            case "app:help":
                QueryBox.Text = "/";
                QueryBox.CaretIndex = QueryBox.Text.Length;
                RenderRows(_engine.Update("/"));
                break;
        }
    }

    private static void SetClipboard(string text) => TrySetClipboard(text);

    private static bool TrySetClipboard(string text)
    {
        // The clipboard is a shared resource; another process can hold it briefly.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                Thread.Sleep(30);
            }
        }
        return false;
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void AddRecentApp(string title)
    {
        _recentApps.Remove(title);
        _recentApps.Insert(0, title);
        while (_recentApps.Count > 5) _recentApps.RemoveAt(_recentApps.Count - 1);
    }

    private void RenderRows(IReadOnlyList<ScoredCandidate> scored)
    {
        _rows.Clear();
        _selected = 0;
        var cap = scored.Count > 0 && scored[0].Candidate.Kind == CandidateKind.Command
            ? PaletteRows
            : MaxRows;
        for (var i = 0; i < scored.Count && i < cap; i++)
            _rows.Add(new RowView(scored[i], i == 0));
        RowsScroll.ScrollToTop();
        UpdateFooter();
    }

    private void UpdateFooter()
    {
        if (!string.IsNullOrEmpty(_flash))
            FooterLeft.Text = _flash;
        else if (!string.IsNullOrEmpty(_lastError))
            FooterLeft.Text = _lastError;
        else if (_services.WebSearch is null &&
                 (QueryBox.Text ?? string.Empty).TrimStart().StartsWith("/web", StringComparison.OrdinalIgnoreCase))
            FooterLeft.Text = "Brave API key not set — opening Google instead";
        else if (!_client.HasKey)
            FooterLeft.Text = "TYPESAFE_API_KEY is not set — local matching only";
        else
            FooterLeft.Text = $"{_stats.LastMs:0} ms · ${_stats.EstimatedCost:0.00000}";

        FooterRight.Text = _rows.Count == 0
            ? string.Empty
            : _rows[0].IsReady ? "↵ ready" : "Ctrl+Enter copy";
        FooterLeft.ToolTip =
            $"p50 {_stats.P50:0} ms · p95 {_stats.P95:0} ms · {_stats.Decisions} decisions · {_stats.InputTokens} tokens";
    }

    private void BuildIndex()    {
        if (Interlocked.Exchange(ref _indexBuildInFlight, 1) == 1) return;
        try
        {
            var index = LocalIndex.Build();
            _engine.SetIndex(index);
            _indexReady = true;
            Dispatcher.Invoke(() =>
            {
                if (IsVisible) RenderRows(_engine.Update(QueryBox.Text ?? string.Empty));
            });
        }
        catch
        {
            // Indexing must never crash the launcher.
        }
        finally
        {
            Interlocked.Exchange(ref _indexBuildInFlight, 0);
        }
    }

    public async Task<string> RunSmokeAsync()
    {
        ShowPanel();
        for (var i = 0; i < 150 && !_indexReady; i++)
            await Task.Delay(100);

        var report = new StringBuilder();
        report.AppendLine($"index ready: {_indexReady}");

        var settingsProbe = new Settings();
        settingsProbe.SetApiKey("smoke-key");
        report.AppendLine("settings key roundtrip => " + (settingsProbe.GetApiKey() == "smoke-key" ? "ok" : "FAILED"));
        report.AppendLine("settings hotkey parse => " + string.Join(",", HotKey.Parse("Alt+Space")));
        try
        {
            var settingsWindow = new SettingsWindow(new Settings()) { Width = 520 };
            settingsWindow.Show();
            settingsWindow.UpdateLayout();
            var root = settingsWindow.SettingsRoot;
            var w = (int)Math.Ceiling(root.ActualWidth);
            var h = (int)Math.Ceiling(root.ActualHeight);
            if (w > 0 && h > 0)
            {
                var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jev-settings.png");
                using var stream = System.IO.File.Create(png);
                encoder.Save(stream);
                report.AppendLine($"settings png => {png} ({w}x{h})");
            }
            settingsWindow.Close();
        }
        catch (Exception ex)
        {
            report.AppendLine("settings png failed => " + ex.Message);
        }

        void Probe(string q)
        {
            RenderRows(_engine.Update(q));
            report.AppendLine($"{q} => {string.Join(" | ", _rows.Take(3).Select(r => r.Title))}");
        }

        Probe("dark");
        Probe("wifi");
        Probe("15+2");

        SetClipboard("smoke-before");
        RenderRows(_engine.Update("15+2"));
        _selected = 0;
        report.AppendLine("15+2 top => " +
            (_rows.Count > 0 ? $"{_rows[0].Candidate.Kind} target={_rows[0].Candidate.Target}" : "none"));
        if (_rows.Count > 0 && _rows[0].Candidate.Kind == CandidateKind.Calculate)
        {
            ExecuteSelected();
        }
        else
        {
            report.AppendLine("15+2 run => skipped (top was not a calculator)");
        }

        string after;
        try { after = Clipboard.GetText(); } catch (Exception ex) { after = "<" + ex.Message + ">"; }
        report.AppendLine("15+2 run => clipboard: " + after);

        Probe("/yt lofi beats");
        Probe("/uuid");
        Probe("/color #f386a1");
        report.AppendLine($"windows => {WindowList.Build().Count}");
        report.AppendLine($"notes => {_services.Notes?.Recent(20).Count ?? 0}");
        report.AppendLine($"clipboard history => {_services.Clipboard.Items.Count}");
        report.AppendLine($"web search => {(_services.WebSearch is null ? "not configured" : "configured")}");

        QueryBox.Text = "dark";
        RenderRows(_engine.Update("dark"));
        Rows.UpdateLayout();
        var container = Rows.ItemContainerGenerator.ContainerFromIndex(0);
        report.AppendLine($"list control => Items={Rows.Items.Count}, first container={(container is null ? "null" : "ok")}");

        try
        {
            RootBorder.UpdateLayout();
            var width = (int)Math.Ceiling(RootBorder.ActualWidth);
            var height = (int)Math.Ceiling(RootBorder.ActualHeight);
            if (width > 0 && height > 0)
            {
                var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(RootBorder);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jev-panel.png");
                using var stream = System.IO.File.Create(png);
                encoder.Save(stream);
                report.AppendLine($"panel png => {png} ({width}x{height})");
            }
            else
            {
                report.AppendLine("panel png => skipped (no size)");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine("panel png failed => " + ex.Message);
        }

        QueryBox.Text = "/";
        RenderRows(_engine.Update("/"));
        RootBorder.UpdateLayout();
        report.AppendLine($"commands palette => {_rows.Count} entries");
        try
        {
            var width = (int)Math.Ceiling(RootBorder.ActualWidth);
            var height = (int)Math.Ceiling(RootBorder.ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(RootBorder);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jev-commands.png");
            using var stream = System.IO.File.Create(png);
            encoder.Save(stream);
            report.AppendLine($"commands png => {png} ({width}x{height})");
        }
        catch (Exception ex)
        {
            report.AppendLine("commands png failed => " + ex.Message);
        }

        QueryBox.Text = "/color #f386a1";
        RenderRows(_engine.Update("/color #f386a1"));
        RootBorder.UpdateLayout();
        try
        {
            var width = (int)Math.Ceiling(RootBorder.ActualWidth);
            var height = (int)Math.Ceiling(RootBorder.ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(RootBorder);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var png = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "jev-utility.png");
            using var stream = System.IO.File.Create(png);
            encoder.Save(stream);
            report.AppendLine($"utility png => {png} ({width}x{height})");
        }
        catch (Exception ex)
        {
            report.AppendLine("utility png failed => " + ex.Message);
        }

        return report.ToString();
    }
}

public sealed class RowView : INotifyPropertyChanged
{
    private bool _isSelected;

    public RowView(ScoredCandidate source, bool selected)
    {
        Source = source;
        _isSelected = selected;
    }

    public ScoredCandidate Source { get; }
    public Candidate Candidate => Source.Candidate;
    public string Title => Candidate.Title;
    public string Detail => Candidate.Detail;
    public string Glyph => GlyphFor(Candidate.Kind);
    public string KindLabel => Candidate.Kind == CandidateKind.Command
        ? string.Empty
        : JevQuestions.KindName(Candidate.Kind);
    public bool IsReady => Source.IsTopReady;
    public Visibility ReadyVisibility => Source.IsTopReady ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string GlyphFor(CandidateKind kind) => kind switch
    {
        CandidateKind.OpenApp => "\uECAA",
        CandidateKind.OpenFile => "\uE8A5",
        CandidateKind.WebSearch => "\uE774",
        CandidateKind.Calculate => "\uE8EF",
        CandidateKind.SystemToggle => "\uE713",
        CandidateKind.RunShortcut => "\uE756",
        CandidateKind.Command => "\uE756",
        CandidateKind.OpenUrl => "\uE774",
        CandidateKind.Copy => "\uE8C8",
        CandidateKind.FocusWindow => "\uE737",
        CandidateKind.SaveNote => "\uE70B",
        CandidateKind.Timer => "\uE916",
        _ => "\uE721",
    };
}
