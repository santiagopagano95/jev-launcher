using JevLauncher.Core;
using Xunit;

public class TimerCommandTests
{
    [Theory]
    [InlineData("5m", 300_000L)]
    [InlineData("30s", 30_000L)]
    [InlineData("1h", 3_600_000L)]
    [InlineData("1h30m", 5_400_000L)]
    [InlineData("90", 90_000L)]
    [InlineData("2 m", 120_000L)]
    public void Parses_durations(string input, long expected)
    {
        Assert.Equal(expected, TimerCommand.TryParseMilliseconds(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("5x")]
    [InlineData("m")]
    public void Rejects_invalid_durations(string input)
    {
        Assert.Null(TimerCommand.TryParseMilliseconds(input));
    }
}
