using JevLauncher.Core;
using Xunit;

public class UtilityCommandsTests
{
    [Fact]
    public void Uuid_produces_a_copy_candidate()
    {
        var candidate = UtilityCommands.Build("uuid", string.Empty).Single();
        Assert.Equal(CandidateKind.Copy, candidate.Kind);
        Assert.True(Guid.TryParse(candidate.Target, out _));
    }

    [Fact]
    public void Uuid_honours_a_count()
    {
        Assert.Equal(3, UtilityCommands.Build("uuid", "3").Count);
    }

    [Fact]
    public void Base64_round_trips()
    {
        var encoded = UtilityCommands.Build("b64", "hola mundo").Single();
        Assert.Equal(CandidateKind.Copy, encoded.Kind);

        var decoded = UtilityCommands.Build("b64d", encoded.Target!).Single();
        Assert.Equal("hola mundo", decoded.Target);
    }

    [Fact]
    public void Base64_decode_rejects_invalid_input()
    {
        Assert.Empty(UtilityCommands.Build("b64d", "!!!not-base64!!!"));
    }

    [Fact]
    public void Hash_is_sha256_hex()
    {
        var candidate = UtilityCommands.Build("hash", "abc").Single();
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", candidate.Target);
    }

    [Theory]
    [InlineData("#f386a1", "#F386A1")]
    [InlineData("f386a1", "#F386A1")]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("  #D45BB6 ", "#D45BB6")]
    public void Normalizes_colors(string input, string expected)
    {
        Assert.Equal(expected, UtilityCommands.Build("color", input).Single().Target);
    }

    [Fact]
    public void Color_rejects_non_hex_input()
    {
        Assert.Empty(UtilityCommands.Build("color", "notacolor"));
    }

    [Fact]
    public void Empty_arguments_produce_nothing()
    {
        Assert.Empty(UtilityCommands.Build("b64", string.Empty));
        Assert.Empty(UtilityCommands.Build("hash", string.Empty));
    }
}
