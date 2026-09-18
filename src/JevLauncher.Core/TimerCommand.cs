namespace JevLauncher.Core;

public static class TimerCommand
{
    private const long MaxMilliseconds = 24 * 60 * 60 * 1000;

    /// <summary>Parses "5m", "30s", "1h", "1h30m" or a bare number of seconds.</summary>
    public static long? TryParseMilliseconds(string? argument)
    {
        var text = (argument ?? string.Empty).Trim().ToLowerInvariant();
        if (text.Length == 0) return null;

        long total = 0;
        var digits = string.Empty;
        var matched = false;

        foreach (var ch in text)
        {
            if (char.IsDigit(ch)) { digits += ch; continue; }
            if (ch == ' ') continue;
            if (digits.Length == 0) return null;

            var value = long.Parse(digits);
            digits = string.Empty;

            var part = ch switch
            {
                's' => value * 1000L,
                'm' => value * 60_000L,
                'h' => value * 3_600_000L,
                _ => -1L,
            };
            if (part < 0) return null;
            total += part;
            matched = true;
        }

        if (digits.Length > 0)
        {
            total += long.Parse(digits) * 1000L; // bare number counts as seconds
            matched = true;
        }

        if (!matched || total <= 0) return null;
        return Math.Min(total, MaxMilliseconds);
    }
}
