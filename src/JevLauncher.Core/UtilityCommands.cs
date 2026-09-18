using System.Security.Cryptography;
using System.Text;

namespace JevLauncher.Core;

public static class UtilityCommands
{
    public static IReadOnlyList<Candidate> Build(string commandId, string argument)
    {
        return commandId.ToLowerInvariant() switch
        {
            "uuid" => Uuid(argument),
            "b64" => Encode(argument),
            "b64d" => Decode(argument),
            "hash" => Hash(argument),
            "color" => Color(argument),
            _ => Array.Empty<Candidate>(),
        };
    }

    private static IReadOnlyList<Candidate> Uuid(string argument)
    {
        var count = int.TryParse(argument.Trim(), out var n) ? Math.Clamp(n, 1, 50) : 1;
        var list = new List<Candidate>(count);
        for (var i = 0; i < count; i++)
        {
            var value = Guid.NewGuid().ToString();
            list.Add(Copy($"UUID: {value}", value, "uuid guid"));
        }
        return list;
    }

    private static IReadOnlyList<Candidate> Encode(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return Array.Empty<Candidate>();
        var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(argument));
        return new[] { Copy($"Base64: {value}", value, "base64 encode") };
    }

    private static IReadOnlyList<Candidate> Decode(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return Array.Empty<Candidate>();
        try
        {
            var value = Encoding.UTF8.GetString(Convert.FromBase64String(argument));
            return new[] { Copy($"Decoded: {Preview(value)}", value, "base64 decode") };
        }
        catch (FormatException)
        {
            return Array.Empty<Candidate>();
        }
    }

    private static IReadOnlyList<Candidate> Hash(string argument)
    {
        if (string.IsNullOrEmpty(argument)) return Array.Empty<Candidate>();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(argument));
        var value = Convert.ToHexString(bytes).ToLowerInvariant();
        return new[] { Copy($"SHA-256: {value}", value, "hash sha256") };
    }

    private static IReadOnlyList<Candidate> Color(string argument)
    {
        var text = argument.Trim().TrimStart('#');
        if (text.Length == 3 && text.All(Uri.IsHexDigit))
            text = string.Concat(text.Select(ch => new string(ch, 2)));

        if (text.Length != 6 || !text.All(Uri.IsHexDigit)) return Array.Empty<Candidate>();

        var hex = "#" + text.ToUpperInvariant();
        var r = Convert.ToInt32(text[..2], 16);
        var g = Convert.ToInt32(text[2..4], 16);
        var b = Convert.ToInt32(text[4..6], 16);
        return new[] { Copy(hex, hex, "color hex rgb", $"RGB({r}, {g}, {b})") };
    }

    private static Candidate Copy(string title, string value, string keywords, string? detail = null) =>
        new("util", CandidateKind.Copy, title, detail ?? "Press Enter to copy", keywords, value);

    private static string Preview(string value)
    {
        var single = value.ReplaceLineEndings(" ");
        return single.Length <= 60 ? single : single[..60] + "…";
    }
}
