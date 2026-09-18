using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace JevLauncher.App;

public sealed class HotKey : IDisposable
{
    public const string Default = "Alt+Space";

    private const int WM_HOTKEY = 0x0312;
    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModWin = 0x0008;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _registered;

    public HotKey(Window window, string hotKey, Action onPressed)
    {
        _onPressed = onPressed;
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);

        var (mods, vk) = Parse(hotKey);
        _registered = mods != 0 && RegisterHotKey(_hwnd, 1, mods, vk);
    }

    public bool IsRegistered => _registered;

    public static (int Mods, uint Vk) Parse(string? hotKey)
    {
        var parts = (hotKey ?? string.Empty)
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return (0, 0);

        var mods = 0;
        foreach (var part in parts[..^1])
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= ModControl; break;
                case "alt": mods |= ModAlt; break;
                case "shift": mods |= ModShift; break;
                case "win": mods |= ModWin; break;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: true, out var key)) return (0, 0);
        return (mods, (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    public static string Capture(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY) { _onPressed(); handled = true; }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered) UnregisterHotKey(_hwnd, 1);
        _source.RemoveHook(WndProc);
    }

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
