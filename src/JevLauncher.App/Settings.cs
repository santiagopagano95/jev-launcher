using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JevLauncher.App;

public sealed class Settings
{
    public const string DefaultSearchTemplate = "https://www.google.com/search?q={0}";

    public string? EncryptedApiKey { get; set; }
    public string HotKey { get; set; } = JevLauncher.App.HotKey.Default;
    public string SearchTemplate { get; set; } = DefaultSearchTemplate;

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JevLauncher", "settings.json");

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
        if (!string.IsNullOrWhiteSpace(env)) return env;
        if (string.IsNullOrWhiteSpace(EncryptedApiKey)) return null;

        try
        {
            var bytes = Convert.FromBase64String(EncryptedApiKey);
            var plain = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    public void SetApiKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            EncryptedApiKey = null;
            return;
        }

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
        EncryptedApiKey = Convert.ToBase64String(bytes);
    }
}
