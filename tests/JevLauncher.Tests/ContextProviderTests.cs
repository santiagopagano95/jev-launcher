using JevLauncher.Core;
using Xunit;

public class ContextProviderTests
{
    [Theory]
    [InlineData(11, "morning")]
    [InlineData(15, "afternoon")]
    [InlineData(20, "evening")]
    [InlineData(3, "night")]
    public void Classifies_time_of_day(int hour, string expected)
    {
        Assert.Equal(expected, ContextProvider.TimeOfDay(new DateTime(2026, 9, 18, hour, 0, 0)));
    }
}
