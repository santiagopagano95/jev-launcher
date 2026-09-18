using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JevLauncher.App;

public static class Acrylic
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public static void Apply(Window window, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var darkVal = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkVal, sizeof(int));

            var corner = 2; // round
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            var backdrop = 3; // Acrylic
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch
        {
            // Backdrop is cosmetic; ignore on unsupported systems.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
