using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
    private readonly List<RowView> _rows = new();
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _debounce;

    private IDisposable? _tray;
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

        _settings = settings;
        _client = new JevClient(new HttpClient(), _settings.GetApiKey());
        _engine = new LauncherEngine(
            Array.Empty<Candidate>(),
            _client,
            _stats,
            () => ContextProvider.Capture(_recentApps, _clipboard));

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
        _lastError = string.Empty;
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height * 0.2;

        Show();
        Activate();
        QueryBox.Focus();
        QueryBox.SelectAll();
        RenderRows(_engine.Update(QueryBox.Text ?? string.Empty));
        _ = Task.Run(BuildIndex);
        Acrylic.Apply(this, dark: true);
    }

    public void HidePanel()
    {
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
        var rows = await _engine.UpdateAsync(query);
        if ((QueryBox.Text ?? string.Empty) != query) return;
        RenderRows(rows);
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
                ExecuteSelected();
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
    }

    private void ExecuteSelected()
    {
        if (_selected < 0 || _selected >= _rows.Count) return;
        var candidate = _rows[_selected].Candidate;

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

    private static void SetClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* clipboard can be locked by another process */ }
    }

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
        for (var i = 0; i < scored.Count; i++)
            _rows.Add(new RowView(scored[i], i == 0));
        Rows.ItemsSource = _rows;
        UpdateFooter();
    }

    private void UpdateFooter()
    {
        if (!string.IsNullOrEmpty(_lastError))
            FooterLeft.Text = _lastError;
        else if (!_client.HasKey)
            FooterLeft.Text = "TYPESAFE_API_KEY is not set — local matching only";
        else
            FooterLeft.Text = $"{_stats.LastMs:0} ms · ${_stats.EstimatedCost:0.00000}";

        FooterRight.Text = _rows.Count > 0 && _rows[0].IsReady ? "↵ ready" : string.Empty;
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

        void Probe(string q)
        {
            RenderRows(_engine.Update(q));
            report.AppendLine($"{q} => {string.Join(" | ", _rows.Take(3).Select(r => r.Title))}");
        }

        Probe("dark");
        Probe("wifi");
        Probe("15+2");

        try { Clipboard.SetText("smoke-before"); } catch { }
        RenderRows(_engine.Update("15+2"));
        _selected = 0;
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
        _ => "\uE721",
    };
}
