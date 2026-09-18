using System.Diagnostics;
using JevLauncher.Core.Native;

namespace JevLauncher.Core;

public interface IClipboardKindProvider
{
    string Kind { get; }
}

public static class ContextProvider
{
    public static LauncherContext Capture(IReadOnlyList<string> recentApps, IClipboardKindProvider clipboard)
    {
        var now = DateTime.Now;
        return new LauncherContext(
            FrontmostApp(),
            recentApps,
            clipboard.Kind,
            TimeOfDay(now),
            now.DayOfWeek.ToString());
    }

    public static string TimeOfDay(DateTime now) => now.Hour switch
    {
        >= 5 and < 12 => "morning",
        >= 12 and < 18 => "afternoon",
        >= 18 and < 22 => "evening",
        _ => "night",
    };

    private static string FrontmostApp()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }
}
