using Microsoft.Win32;

namespace JevLauncher.Core;

public static class Protocols
{
    /// <summary>True when Windows has a registered handler for the URI scheme (e.g. "spotify").</summary>
    public static bool IsRegistered(string? scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme)) return false;

        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(scheme);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }
}
