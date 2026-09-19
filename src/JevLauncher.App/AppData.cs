using System.IO;

namespace JevLauncher.App;

/// <summary>Runtime data folder. Overridable with JEV_LAUNCHER_DATA (used for isolated testing).</summary>
public static class AppData
{
    public static string Directory =>
        Environment.GetEnvironmentVariable("JEV_LAUNCHER_DATA") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JevLauncher");
}

/// <summary>Where smoke reports, crash dumps and debug logs go (inside the app folder).</summary>
public static class Artifacts
{
    public static string Directory =>
        Environment.GetEnvironmentVariable("JEV_ARTIFACTS") is { Length: > 0 } custom
            ? custom
            : Path.Combine(AppContext.BaseDirectory, "artifacts");
}
