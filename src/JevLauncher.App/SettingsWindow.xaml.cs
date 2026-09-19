using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;

        TemplateBox.Text = settings.SearchTemplate;
        HotkeyBox.Text = settings.HotKey;
        SnippetsBox.Text = string.Join(Environment.NewLine,
            settings.Snippets.Select(s => $"{s.Name} = {s.Text}"));
        StartWithWindowsBox.IsChecked = settings.StartWithWindows;
        FoldersBox.Text = string.Join(Environment.NewLine, settings.IndexFolders);

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TYPESAFE_API_KEY")))
            EnvNote.Text = "TYPESAFE_API_KEY is set in the environment and takes precedence over the stored key.";
        else if (settings.GetApiKey() is { } stored)
            ApiKeyBox.Password = stored;
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin)
            return;

        HotkeyBox.Text = HotKey.Capture(Keyboard.Modifiers, key);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _settings.SetApiKey(string.IsNullOrWhiteSpace(ApiKeyBox.Password) ? null : ApiKeyBox.Password);
        _settings.HotKey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? HotKey.Default : HotkeyBox.Text;
        _settings.SearchTemplate = string.IsNullOrWhiteSpace(TemplateBox.Text)
            ? Settings.DefaultSearchTemplate
            : TemplateBox.Text;
        _settings.Snippets = ParseSnippets(SnippetsBox.Text);
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.IndexFolders = ParseFolders(FoldersBox.Text);
        DialogResult = true;
    }

    private static List<string> ParseFolders(string text) =>
        text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<Snippet> ParseSnippets(string text)
    {
        var list = new List<Snippet>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var equals = line.IndexOf('=');
            if (equals <= 0) continue;

            var name = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0) continue;

            list.Add(new Snippet(name, value));
        }
        return list;
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        TestResult.Foreground = System.Windows.Media.Brushes.Gray;
        TestResult.Text = "Testing…";
        try
        {
            var key = string.IsNullOrWhiteSpace(ApiKeyBox.Password) ? null : ApiKeyBox.Password;
            var client = new JevClient(new HttpClient(), key);
            var conversation = new Conversation(
                new[] { new Candidate("c0", CandidateKind.OpenApp, "Calculator", "Application", "calc", "calc.exe") },
                new LauncherContext("test", Array.Empty<string>(), "text", "afternoon", "Thursday"));

            var sw = Stopwatch.StartNew();
            var response = await client.QueryAsync("calc", conversation);
            sw.Stop();

            if (response is null)
            {
                TestResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
                TestResult.Text = "No response — check the key (or there is no network).";
            }
            else
            {
                TestResult.Foreground = System.Windows.Media.Brushes.MediumAquamarine;
                TestResult.Text = $"OK · {sw.ElapsedMilliseconds} ms · {response.InputTokens} input tokens";
            }
        }
        catch (Exception ex)
        {
            TestResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
            TestResult.Text = "Error: " + ex.Message;
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }
}
