using JevLauncher.Core;
using Xunit;

public class CalculatorTests
{
    [Theory]
    [InlineData("15% of 240", 36)]
    [InlineData("200 * 10%", 20)]
    [InlineData("2 + 2", 4)]
    [InlineData("2^3", 8)]
    [InlineData("sqrt 16", 4)]
    [InlineData("calc 1+1", 2)]
    [InlineData("= 3 * (4 + 1)", 15)]
    [InlineData("6 x 7", 42)]
    [InlineData("2 ** 3", 8)]
    public void Parses_arithmetic(string query, double expected)
    {
        Assert.True(Calculator.TryParse(query, out var value));
        Assert.Equal(expected, value, 6);
    }

    [Theory]
    [InlineData("the pdf I just downloaded")]
    [InlineData("dark")]
    [InlineData("")]
    [InlineData("wifi off")]
    public void Rejects_non_arithmetic(string query)
    {
        Assert.False(Calculator.TryParse(query, out _));
    }
}
