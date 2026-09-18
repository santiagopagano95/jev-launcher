using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JevLauncher.App;

public sealed class HotKey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int MOD_ALT = 0x0001;
    private const uint VK_SPACE = 0x20;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _registered;

    public HotKey(Window window, Action onPressed)
    {
        _onPressed = onPressed;
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd)!;
        _source.AddHook(WndProc);
        _registered = RegisterHotKey(_hwnd, 1, MOD_ALT, VK_SPACE);
    }

    public bool IsRegistered => _registered;

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
