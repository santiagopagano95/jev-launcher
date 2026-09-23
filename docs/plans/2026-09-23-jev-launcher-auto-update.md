# Auto-update from GitHub Releases Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** The launcher checks GitHub Releases, notifies when a newer version exists, and on consent downloads the Setup, verifies its SHA256, and applies it silently.

**Architecture:** Pure, testable update logic lives in `JevLauncher.Core` (`UpdateChecker`, `UpdateDownloader`). The WPF app owns scheduling, the consent dialog, and applying the update. A GitHub Actions workflow publishes releases on `v*` tags.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), WPF, xUnit, `System.Text.Json`, `System.Security.Cryptography`, GitHub Actions.

**Design doc:** `docs/plans/2026-09-23-jev-launcher-auto-update-design.md`

---

### Task 1: `UpdateChecker` release parsing (Core)

**Files:**
- Create: `src/JevLauncher.Core/UpdateChecker.cs`
- Create: `tests/JevLauncher.Tests/UpdateCheckerTests.cs`

**Step 1: Write the failing tests**

Create `tests/JevLauncher.Tests/UpdateCheckerTests.cs`:

```csharp
using JevLauncher.Core;
using Xunit;

public class UpdateCheckerTests
{
    private const string ReleaseJson = """
    {
      "tag_name": "v1.1.0",
      "html_url": "https://github.com/santiagopagano95/jev-launcher/releases/tag/v1.1.0",
      "body": "Notas",
      "assets": [
        { "name": "JevLauncher-1.1.0.zip", "browser_download_url": "https://example/zip" },
        { "name": "JevLauncher-Setup-1.1.0.exe", "browser_download_url": "https://example/setup" },
        { "name": "JevLauncher-Setup-1.1.0.exe.sha256", "browser_download_url": "https://example/sum" }
      ]
    }
    """;

    [Fact]
    public void Parses_tag_version_and_assets()
    {
        var release = UpdateChecker.ParseRelease(ReleaseJson);

        Assert.NotNull(release);
        Assert.Equal("v1.1.0", release!.Tag);
        Assert.Equal(new Version(1, 1, 0), release.Version);
        Assert.Equal("https://example/setup", release.SetupUrl);
        Assert.Equal("https://example/sum", release.ChecksumUrl);
        Assert.Equal("Notas", release.Notes);
    }

    [Fact]
    public void Returns_null_when_checksum_asset_missing()
    {
        var json = ReleaseJson.Replace(
            ", { \"name\": \"JevLauncher-Setup-1.1.0.exe.sha256\", \"browser_download_url\": \"https://example/sum\" }",
            "");
        Assert.Null(UpdateChecker.ParseRelease(json));
    }

    [Fact]
    public void Returns_null_on_invalid_json()
    {
        Assert.Null(UpdateChecker.ParseRelease("{ not json"));
    }

    [Theory]
    [InlineData("v1.1.0", 1, 1, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v2.0", 2, 0, -1)]
    public void Parses_tag_versions(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~UpdateCheckerTests"`
Expected: FAIL (UpdateChecker does not exist).

**Step 3: Implement `UpdateChecker`**

Create `src/JevLauncher.Core/UpdateChecker.cs`:

```csharp
using System.Net.Http;
using System.Text.Json;

namespace JevLauncher.Core;

public sealed record ReleaseInfo(
    string Tag,
    Version Version,
    string SetupUrl,
    string ChecksumUrl,
    string HtmlUrl,
    string Notes);

public sealed class UpdateChecker
{
    public const string LatestReleaseUrl =
        "https://api.github.com/repos/santiagopagano95/jev-launcher/releases/latest";

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient http) => _http = http;

    public async Task<ReleaseInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd("JevLauncher");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            var release = ParseRelease(body);
            return release is not null && release.Version > current ? release : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public static ReleaseInfo? ParseRelease(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() ?? "" : "";
            if (!TryParseVersion(tag, out var version)) return null;

            var htmlUrl = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";

            string? setupUrl = null;
            string? checksumUrl = null;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    if (name.StartsWith("JevLauncher-Setup-", StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase))
                        checksumUrl = url;
                    else if (name.StartsWith("JevLauncher-Setup-", StringComparison.OrdinalIgnoreCase) &&
                             name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        setupUrl = url;
                }
            }

            if (string.IsNullOrEmpty(setupUrl) || string.IsNullOrEmpty(checksumUrl)) return null;

            return new ReleaseInfo(tag, version!, setupUrl, checksumUrl, htmlUrl, notes);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool TryParseVersion(string tag, out Version? version)
        => Version.TryParse(tag.TrimStart('v', 'V'), out version);
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~UpdateCheckerTests"`
Expected: PASS.

**Step 5: Commit**

```bash
git add src/JevLauncher.Core/UpdateChecker.cs tests/JevLauncher.Tests/UpdateCheckerTests.cs
git commit -m "feat(update): parse GitHub release metadata"
```

---

### Task 2: `UpdateChecker.CheckAsync` with HTTP

**Files:**
- Modify: `tests/JevLauncher.Tests/UpdateCheckerTests.cs`

**Step 1: Add the failing tests**

Append to `UpdateCheckerTests`:

```csharp
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_f(request));
    }

    [Fact]
    public async Task Returns_release_when_newer()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(ReleaseJson, System.Text.Encoding.UTF8, "application/json"),
        });
        var checker = new UpdateChecker(new HttpClient(handler));

        var release = await checker.CheckAsync(new Version(1, 0, 0));

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 1, 0), release!.Version);
    }

    [Fact]
    public async Task Returns_null_when_current()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(ReleaseJson, System.Text.Encoding.UTF8, "application/json"),
        });
        var checker = new UpdateChecker(new HttpClient(handler));

        Assert.Null(await checker.CheckAsync(new Version(1, 1, 0)));
        Assert.Null(await checker.CheckAsync(new Version(2, 0, 0)));
    }

    [Fact]
    public async Task Returns_null_on_http_error()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));
        var checker = new UpdateChecker(new HttpClient(handler));

        Assert.Null(await checker.CheckAsync(new Version(1, 0, 0)));
    }
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~UpdateCheckerTests"`
Expected: PASS (implementation from Task 1 already covers this).

**Step 3: Commit**

```bash
git add tests/JevLauncher.Tests/UpdateCheckerTests.cs
git commit -m "test(update): cover CheckAsync version gating"
```

---

### Task 3: `UpdateDownloader` with checksum verification

**Files:**
- Create: `src/JevLauncher.Core/UpdateDownloader.cs`
- Create: `tests/JevLauncher.Tests/UpdateDownloaderTests.cs`

**Step 1: Write the failing tests**

Create `tests/JevLauncher.Tests/UpdateDownloaderTests.cs`:

```csharp
using System.Net;
using System.Security.Cryptography;
using System.Text;
using JevLauncher.Core;
using Xunit;

public class UpdateDownloaderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _f;
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) => _f = f;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(_f(request));
    }

    private static ReleaseInfo Release() => new(
        "v1.1.0", new Version(1, 1, 0),
        "https://example/setup",
        "https://example/sum",
        "https://example/html",
        "notas");

    private static (UpdateDownloader downloader, string dir) Make(byte[] payload, bool corrupt)
    {
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var checksum = corrupt ? new string('0', 64) : hash;
        var handler = new FakeHandler(req => req.RequestUri!.ToString() switch
        {
            "https://example/setup" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) },
            "https://example/sum" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{checksum}  setup.exe") },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var dir = Path.Combine(Path.GetTempPath(), "jev-update-test-" + Guid.NewGuid().ToString("N"));
        return (new UpdateDownloader(new HttpClient(handler)), dir);
    }

    [Fact]
    public async Task Downloads_and_verifies_checksum()
    {
        var payload = Encoding.UTF8.GetBytes("fake installer bytes");
        var (downloader, dir) = Make(payload, corrupt: false);
        try
        {
            var path = await downloader.DownloadAsync(Release(), dir);
            Assert.True(File.Exists(path));
            Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task Throws_and_writes_nothing_on_bad_checksum()
    {
        var payload = Encoding.UTF8.GetBytes("fake installer bytes");
        var (downloader, dir) = Make(payload, corrupt: true);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => downloader.DownloadAsync(Release(), dir));
            Assert.False(Directory.Exists(dir) && Directory.GetFiles(dir).Length > 0);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~UpdateDownloaderTests"`
Expected: FAIL (UpdateDownloader does not exist).

**Step 3: Implement `UpdateDownloader`**

Create `src/JevLauncher.Core/UpdateDownloader.cs`:

```csharp
using System.Net.Http;
using System.Security.Cryptography;

namespace JevLauncher.Core;

public sealed class UpdateDownloader
{
    private readonly HttpClient _http;

    public UpdateDownloader(HttpClient http) => _http = http;

    public async Task<string> DownloadAsync(ReleaseInfo release, string destinationDirectory, CancellationToken ct = default)
    {
        var expected = await ReadChecksumAsync(release.ChecksumUrl, ct)
            ?? throw new InvalidOperationException("No se pudo leer el checksum del release.");

        var bytes = await _http.GetByteArrayAsync(release.SetupUrl, ct);
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El checksum del instalador no coincide.");

        Directory.CreateDirectory(destinationDirectory);
        var path = Path.Combine(destinationDirectory, $"JevLauncher-Setup-{release.Version}.exe");
        await File.WriteAllBytesAsync(path, bytes, ct);
        return path;
    }

    private async Task<string?> ReadChecksumAsync(string url, CancellationToken ct)
    {
        try
        {
            var text = await _http.GetStringAsync(url, ct);
            var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return token is { Length: 64 } ? token : null;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj --filter "FullyQualifiedName~UpdateDownloaderTests"`
Expected: PASS.

**Step 5: Commit**

```bash
git add src/JevLauncher.Core/UpdateDownloader.cs tests/JevLauncher.Tests/UpdateDownloaderTests.cs
git commit -m "feat(update): download installer and verify sha256"
```

---

### Task 4: `Settings.CheckForUpdates` + Settings checkbox

**Files:**
- Modify: `src/JevLauncher.App/Settings.cs:18`
- Modify: `src/JevLauncher.App/SettingsWindow.xaml:75-76`
- Modify: `src/JevLauncher.App/SettingsWindow.xaml.cs`

**Step 1: Add the setting**

In `Settings.cs`, after `IndexFolders`:

```csharp
    public bool CheckForUpdates { get; set; } = true;
```

**Step 2: Add the checkbox**

In `SettingsWindow.xaml`, after the `StartWithWindowsBox` block:

```xml
    <CheckBox x:Name="CheckForUpdatesBox" Content="Check for updates automatically" Margin="0,0,0,16"
              Foreground="{StaticResource TextSecondary}" FontFamily="{StaticResource UiFont}" />
```

**Step 3: Wire load/save**

In `SettingsWindow.xaml.cs`, in the constructor where `StartWithWindowsBox.IsChecked` is set, add `CheckForUpdatesBox.IsChecked = settings.CheckForUpdates;`, and in `OnSaveClick` add `settings.CheckForUpdates = CheckForUpdatesBox.IsChecked == true;`. (Follow the exact existing pattern used for `StartWithWindowsBox`.)

**Step 4: Build and run tests**

Run: `dotnet build && dotnet test tests/JevLauncher.Tests/JevLauncher.Tests.csproj`
Expected: build OK, all tests pass.

**Step 5: Commit**

```bash
git add src/JevLauncher.App/Settings.cs src/JevLauncher.App/SettingsWindow.xaml src/JevLauncher.App/SettingsWindow.xaml.cs
git commit -m "feat(update): add CheckForUpdates setting"
```

---

### Task 5: Update consent dialog

**Files:**
- Create: `src/JevLauncher.App/UpdateWindow.xaml`
- Create: `src/JevLauncher.App/UpdateWindow.xaml.cs`

**Step 1: Create the dialog XAML**

`UpdateWindow.xaml` (mirrors `SettingsWindow` styling):

```xml
<Window x:Class="JevLauncher.App.UpdateWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Jev Launcher — Update" Width="520" SizeToContent="Height"
        WindowStartupLocation="CenterScreen" ResizeMode="NoResize"
        Background="{StaticResource PanelSolid}">
  <StackPanel Margin="20">
    <TextBlock x:Name="VersionText" FontFamily="{StaticResource UiFont}" FontSize="15"
               Foreground="{StaticResource TextPrimary}" Margin="0,0,0,8" />
    <TextBlock x:Name="NotesText" FontFamily="{StaticResource MonoFont}" FontSize="11"
               Foreground="{StaticResource TextMuted}" TextWrapping="Wrap"
               MaxHeight="220" Margin="0,0,0,16" />
    <TextBlock x:Name="StatusText" FontFamily="{StaticResource MonoFont}" FontSize="11"
               Foreground="{StaticResource TextMuted}" TextWrapping="Wrap" Margin="0,0,0,16" />
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
      <Button x:Name="NotesButton" Content="Ver cambios" Style="{StaticResource SecondaryButton}"
              Click="OnNotesClick" Margin="0,0,8,0" />
      <Button x:Name="LaterButton" Content="Más tarde" Style="{StaticResource SecondaryButton}"
              Click="OnLaterClick" Margin="0,0,8,0" />
      <Button x:Name="UpdateButton" Content="Actualizar ahora" Style="{StaticResource PrimaryButton}"
              Click="OnUpdateClick" />
    </StackPanel>
  </StackPanel>
</Window>
```

**Step 2: Create the code-behind**

`UpdateWindow.xaml.cs`:

```csharp
using System.Diagnostics;
using System.Windows;
using JevLauncher.Core;

namespace JevLauncher.App;

public partial class UpdateWindow : Window
{
    private readonly ReleaseInfo _release;
    private readonly UpdateDownloader _downloader;
    private readonly string _destinationDirectory;

    public bool Accepted { get; private set; }
    public string? SetupPath { get; private set; }

    public UpdateWindow(ReleaseInfo release, UpdateDownloader downloader, string destinationDirectory)
    {
        InitializeComponent();
        _release = release;
        _downloader = downloader;
        _destinationDirectory = destinationDirectory;
        VersionText.Text = $"Hay una versión nueva: {release.Tag}";
        NotesText.Text = release.Notes;
    }

    private void OnNotesClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_release.HtmlUrl))
            Process.Start(new ProcessStartInfo(_release.HtmlUrl) { UseShellExecute = true });
    }

    private void OnLaterClick(object sender, RoutedEventArgs e) => Close();

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        StatusText.Text = "Descargando…";
        try
        {
            SetupPath = await _downloader.DownloadAsync(_release, _destinationDirectory);
            StatusText.Text = "Instalando…";
            Accepted = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "No se pudo actualizar: " + ex.Message;
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }
}
```

**Step 3: Build**

Run: `dotnet build`
Expected: build OK.

**Step 4: Commit**

```bash
git add src/JevLauncher.App/UpdateWindow.xaml src/JevLauncher.App/UpdateWindow.xaml.cs
git commit -m "feat(update): add update consent dialog"
```

---

### Task 6: `Updater` orchestration + tray item + startup wiring

**Files:**
- Create: `src/JevLauncher.App/Updater.cs`
- Modify: `src/JevLauncher.App/TrayIcon.cs:10-23`
- Modify: `src/JevLauncher.App/PanelWindow.xaml.cs:48,93-105`
- Modify: `src/JevLauncher.App/App.xaml.cs:82-113`

**Step 1: Create `Updater`**

`Updater.cs`:

```csharp
using System.Diagnostics;
using System.IO;
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
    private readonly HttpClient _http = new();
    private readonly UpdateChecker _checker;
    private readonly UpdateDownloader _downloader;
    private readonly DispatcherTimer _timer;
    private bool _busy;

    public Updater(Settings settings)
    {
        _settings = settings;
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
        var release = await _checker.CheckAsync(CurrentVersion);
        if (release is null)
        {
            App.DebugLog("update: up to date");
            return;
        }
        PromptAndApply(release);
    }

    private async Task CheckAndPromptAsync()
    {
        if (_busy) return;
        var release = await _checker.CheckAsync(CurrentVersion);
        if (release is null) return;
        PromptAndApply(release);
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
```

**Step 2: Add the tray menu item**

In `TrayIcon.Attach`, change the signature to `Attach(Window window, Action toggle, Action settings, Action checkUpdates, Action quit)` and add:

```csharp
        var updateItem = new MenuItem { Header = "Buscar actualizaciones" };
        updateItem.Click += (_, _) => checkUpdates();
```
inserted before the separator.

**Step 3: Update call sites**

In `PanelWindow.xaml.cs`: add `public Action? CheckUpdatesAction { get; set; }` next to `OpenSettingsAction`, and in `CreateTrayIcon` pass `() => CheckUpdatesAction?.Invoke()` as the new argument.

In `App.xaml.cs`: in the smoke path, `TrayIcon.Attach(_panel!, static () => { }, static () => { }, static () => { }, static () => { })`. In `OnStartup` after `_panel.CreateTrayIcon();`, create the updater and wire it:

```csharp
        _updater = new Updater(_settings);
        _panel.CheckUpdatesAction = () => _ = _updater.CheckManuallyAsync();
        _updater.StartAutoCheck();
```

Add the field `private Updater? _updater;`.

**Step 4: Build and smoke test**

Run: `dotnet build`
Then: `dotnet run --project src/JevLauncher.App -- --smoke`
Expected: build OK; smoke report still generated with no crash.

**Step 5: Commit**

```bash
git add src/JevLauncher.App/Updater.cs src/JevLauncher.App/TrayIcon.cs src/JevLauncher.App/PanelWindow.xaml.cs src/JevLauncher.App/App.xaml.cs
git commit -m "feat(update): wire auto-check, tray item and apply flow"
```

---

### Task 7: GitHub Actions release workflow

**Files:**
- Create: `.github/workflows/release.yml`

**Step 1: Create the workflow**

```yaml
name: release

on:
  push:
    tags: ['v*']

permissions:
  contents: write

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Install Inno Setup 6
        run: choco install innosetup --no-progress -y
      - name: Compute version
        id: ver
        shell: pwsh
        run: |
          $v = "${{ github.ref_name }}".TrimStart('v','V')
          "version=$v" >> $env:GITHUB_OUTPUT
      - name: Build ZIP and Setup
        shell: pwsh
        run: |
          ./installer/build-all.ps1 -Version "${{ steps.ver.outputs.version }}" `
            -Iscc "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
      - name: Write checksum
        shell: pwsh
        run: |
          $v = "${{ steps.ver.outputs.version }}"
          $setup = "installer/dist/JevLauncher-Setup-$v.exe"
          $hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
          "$hash  $(Split-Path $setup -Leaf)" | Out-File -Encoding ascii "$setup.sha256"
      - name: Publish release
        shell: pwsh
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          $v = "${{ steps.ver.outputs.version }}"
          gh release create "${{ github.ref_name }}" `
            "installer/dist/JevLauncher-Setup-$v.exe" `
            "installer/dist/JevLauncher-Setup-$v.exe.sha256" `
            "installer/dist/JevLauncher-$v.zip" `
            --title "${{ github.ref_name }}" --generate-notes
```

**Step 2: Validate YAML syntax**

Run: `python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/release.yml'))"` (or review manually if Python is unavailable).
Expected: parses without error.

**Step 3: Commit**

```bash
git add .github/workflows/release.yml
git commit -m "ci(update): publish releases on v* tags"
```

---

### Task 8: Documentation

**Files:**
- Modify: `README.md`

**Step 1: Add an "Actualizaciones" section**

Document that the launcher checks GitHub Releases at startup and every 6 hours, notifies, verifies the SHA256 and applies silently; how to cut a release (push a `v*` tag); and the `CheckForUpdates` setting.

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs(update): document auto-update and releases"
```

---

### Task 9: Final verification

**Step 1: Full suite**

Run: `dotnet test`
Expected: all tests pass (existing 192 + the new update tests).

**Step 2: Smoke**

Run: `dotnet run --project src/JevLauncher.App -- --smoke`
Expected: smoke report generated, no crash, no network dependency.

**Step 3: Build artifacts**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File installer/build-all.ps1`
Expected: `OK: los binarios de ejecucion del ZIP y del Setup coinciden.`

**Step 4: Confirm clean tree**

Run: `git status --short`
Expected: no uncommitted changes.
