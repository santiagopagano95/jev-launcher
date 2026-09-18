using System.Runtime.InteropServices;
using System.Text;

namespace JevLauncher.Core;

public static class WindowList
{
    private const int SW_RESTORE = 9;

    public static IReadOnlyList<Candidate> Build()
    {
        var list = new List<Candidate>();
        var ownPid = (uint)Environment.ProcessId;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;

            var length = GetWindowTextLength(hwnd);
            if (length == 0) return true;

            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == ownPid) return true;

            var buffer = new StringBuilder(length + 1);
            GetWindowText(hwnd, buffer, buffer.Capacity);
            var title = buffer.ToString().Trim();
            if (title.Length == 0) return true;

            var handle = hwnd.ToInt64();
            list.Add(new Candidate($"win:{handle}", CandidateKind.FocusWindow, title, "Window", title, handle.ToString()));
            return true;
        }, IntPtr.Zero);

        return list;
    }

    public static void Focus(string? handleText)
    {
        if (!long.TryParse(handleText, out var value)) return;
        var hwnd = new IntPtr(value);
        if (hwnd == IntPtr.Zero) return;

        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        SetForegroundWindow(hwnd);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
