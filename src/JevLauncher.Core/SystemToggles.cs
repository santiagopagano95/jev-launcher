using System.Diagnostics;
using System.Text.RegularExpressions;
using JevLauncher.Core.Native;
using Microsoft.Win32;

namespace JevLauncher.Core;

public sealed record SystemToggle(string Id, string Title, string Detail, string Keywords);

public static class SystemToggles
{
    public static readonly IReadOnlyList<SystemToggle> All = new[]
    {
        new SystemToggle("dark-mode", "Toggle Dark Mode", "System toggle", "dark light mode appearance theme"),
        new SystemToggle("wifi", "Toggle Wi-Fi", "System toggle", "wifi wireless network internet"),
        new SystemToggle("sleep", "Sleep", "System toggle", "sleep suspend power"),
        new SystemToggle("lock", "Lock Screen", "System toggle", "lock screen secure"),
        new SystemToggle("empty-trash", "Empty Recycle Bin", "System toggle", "empty trash recycle bin delete"),
        new SystemToggle("hidden-files", "Toggle Hidden Files", "System toggle", "hidden files show hide explorer"),
        new SystemToggle("mute", "Toggle Mute", "System toggle", "mute volume sound audio"),
    };

    public static IReadOnlyList<Candidate> BuildCandidates() =>
        All.Select(t => new Candidate(t.Id, CandidateKind.SystemToggle, t.Title, t.Detail, t.Keywords, t.Id)).ToList();

    public static string? ParseWifiInterface(string netshOutput)
    {
        var match = Regex.Match(netshOutput, @"^\s*Name\s*:\s*(.+?)\s*$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static void Execute(string id, Action<string> reportError)
    {
        switch (id)
        {
            case "dark-mode": ToggleDarkMode(); break;
            case "wifi": ToggleWifi(reportError); break;
            case "sleep": NativeMethods.SetSuspendState(false, false, false); break;
            case "lock": NativeMethods.LockWorkStation(); break;
            case "empty-trash": NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, 0); break;
            case "hidden-files": ToggleHiddenFiles(); break;
            case "mute": AudioMute.Toggle(); break;
        }
    }

    private static void ToggleDarkMode()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        using var k = Registry.CurrentUser.OpenSubKey(key, writable: true)!;
        var current = k.GetValue("AppsUseLightTheme") as int? ?? 1;
        var next = current == 0 ? 1 : 0;
        k.SetValue("AppsUseLightTheme", next, RegistryValueKind.DWord);
        k.SetValue("SystemUsesLightTheme", next, RegistryValueKind.DWord);
        NativeMethods.SendMessageTimeout((IntPtr)NativeMethods.HWND_BROADCAST, NativeMethods.WM_SETTINGCHANGE,
            IntPtr.Zero, "ImmersiveColorSet", NativeMethods.SMTO_ABORTIFHUNG, 1000, out _);
    }

    private static void ToggleHiddenFiles()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        using var k = Registry.CurrentUser.OpenSubKey(key, writable: true)!;
        var current = k.GetValue("Hidden") as int? ?? 2;
        k.SetValue("Hidden", current == 1 ? 2 : 1, RegistryValueKind.DWord);
        NativeMethods.SHChangeNotify(NativeMethods.SHCNE_ASSOCCHANGED, NativeMethods.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    private static void ToggleWifi(Action<string> reportError)
    {
        var psi = new ProcessStartInfo("netsh", "interface show interface")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc is null) { reportError("Could not query network interfaces."); return; }
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        var name = ParseWifiInterface(output);
        if (name is null) { reportError("No Wi-Fi interface found."); return; }

        var state = new ProcessStartInfo("netsh", $"interface show interface name=\"{name}\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var stateProc = Process.Start(state)!;
        var stateOut = stateProc.StandardOutput.ReadToEnd();
        stateProc.WaitForExit();
        var isEnabled = stateOut.Contains("Enabled", StringComparison.OrdinalIgnoreCase);

        var action = isEnabled ? "admin=disabled" : "admin=enabled";
        var run = new ProcessStartInfo("netsh", $"interface set interface name=\"{name}\" {action}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var runProc = Process.Start(run);
        runProc?.WaitForExit();
        if (runProc is null || runProc.ExitCode != 0)
            reportError("Wi-Fi toggle needs administrator rights.");
    }
}
