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

    [Theory]
    [InlineData("x")]
    [InlineData("reddit")]
    [InlineData("spotify")]
    [InlineData("netflix")]
    [InlineData("snip")]
    [InlineData("clip")]
    [InlineData("uuid")]
    [InlineData("b64")]
    [InlineData("b64d")]
    [InlineData("hash")]
    [InlineData("color")]
    [InlineData("win")]
    [InlineData("note")]
    [InlineData("timer")]
    public void Resolves_new_commands(string name)
    {
        Assert.NotNull(CommandParser.Resolve(name));
    }

    [Fact]
    public void Every_home_url_is_https()
    {
        var withHome = Commands.All.Where(c => c.HomeUrl is not null).ToList();
        Assert.NotEmpty(withHome);
        foreach (var command in withHome)
            Assert.StartsWith("https://", command.HomeUrl);
    }

    [Fact]
    public void Web_command_uses_the_web_results_scope()
    {
        var web = CommandParser.Resolve("web")!;
        Assert.Equal(CommandScope.WebResults, web.Scope);
        Assert.Contains("{0}", web.UrlTemplate); // browser fallback
    }

    [Theory]
    [InlineData("search")]
    [InlineData("s")]
    public void Web_command_aliases_resolve(string alias)
    {
        Assert.Equal("web", CommandParser.Resolve(alias)?.Id);
    }
}
