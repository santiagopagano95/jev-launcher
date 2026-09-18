using JevLauncher.Core;
using Xunit;

public class CommandsTests
{
    [Fact]
    public void Resolves_by_id_case_insensitive()
    {
        Assert.Equal("web", CommandParser.Resolve("WEB")?.Id);
    }

    [Fact]
    public void Resolves_by_alias()
    {
        Assert.Equal("web", CommandParser.Resolve("g")?.Id);
        Assert.Equal("wiki", CommandParser.Resolve("w")?.Id);
        Assert.Equal("toggle", CommandParser.Resolve("toggles")?.Id);
    }

    [Fact]
    public void Unknown_command_is_null()
    {
        Assert.Null(CommandParser.Resolve("nope"));
        Assert.Null(CommandParser.Resolve(""));
    }

    [Theory]
    [InlineData("/web", "web", "")]
    [InlineData("/web hello world", "web", "hello world")]
    [InlineData("/", "", "")]
    [InlineData("/yt   lofi", "yt", "lofi")]
    public void Splits_name_and_argument(string query, string name, string argument)
    {
        var (n, a) = CommandParser.Split(query);
        Assert.Equal(name, n);
        Assert.Equal(argument, a);
    }

    [Fact]
    public void Every_web_command_has_a_template_with_placeholder()
    {
        foreach (var command in Commands.All.Where(c => c.Scope == CommandScope.Web))
            Assert.Contains("{0}", command.UrlTemplate);
    }
}
