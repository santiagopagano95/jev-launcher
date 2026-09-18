using JevLauncher.Core;
using Xunit;

public class SystemTogglesTests
{
    [Fact]
    public void Provides_the_seven_required_toggles()
    {
        var ids = SystemToggles.All.Select(t => t.Id).ToHashSet();
        Assert.Superset(new HashSet<string>
        {
            "dark-mode", "wifi", "sleep", "lock", "empty-trash", "hidden-files", "mute",
        }, ids);
    }

    [Fact]
    public void Toggle_candidates_are_system_toggles()
    {
        Assert.All(SystemToggles.BuildCandidates(), c => Assert.Equal(CandidateKind.SystemToggle, c.Kind));
    }

    [Fact]
    public void Parses_wifi_interface_name_from_netsh_output()
    {
        const string output = """
        Name                   : Wi-Fi
        Description            : Intel(R) Wi-Fi 6
        State                  : connected
        """;
        Assert.Equal("Wi-Fi", SystemToggles.ParseWifiInterface(output));
    }
}
