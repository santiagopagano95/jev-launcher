using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JevLauncher.Core;

namespace JevLauncher.App;

public sealed class Settings
{
    public const string DefaultSearchTemplate = "https://www.google.com/search?q={0}";

    public string? EncryptedApiKey { get; set; }
    public string HotKey { get; set; } = JevLauncher.App.HotKey.Default;
    public string SearchTemplate { get; set; } = DefaultSearchTemplate;
    public List<Snippet> Snippets { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
    public List<string> IndexFolders { get; set; } = new();
    public bool CheckForUpdates { get; set; } = true;

    public static string FilePath => Path.Combine(AppData.Directory, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // Corrupt or unreadable settings: fall back to defaults.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Settings are best-effort; never crash the launcher.
        }
    }

    public string? GetApiKey()
    {
        var env = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
        return !string.IsNullOrWhiteSpace(env) ? env : Unprotect(EncryptedApiKey);
    }

    public void SetApiKey(string? key) => EncryptedApiKey = Protect(key);

    private static string? Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string? Unprotect(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return null;
        try
        {
            var bytes = Convert.FromBase64String(encrypted);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return null;
        }
    }
}
