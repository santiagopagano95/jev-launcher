using JevLauncher.Core;
using Xunit;

public class UpdateApplyTests
{
    [Fact]
    public void Quotes_paths_with_spaces()
    {
        var args = UpdateApply.BuildCommandArguments(
            @"C:\Users\John Doe\JevLauncher-Setup-1.1.0.exe",
            @"C:\Program Files\Jev Launcher\JevLauncher.App.exe");

        Assert.Equal(
            "/c \"\"C:\\Users\\John Doe\\JevLauncher-Setup-1.1.0.exe\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART && start \"\" \"C:\\Program Files\\Jev Launcher\\JevLauncher.App.exe\"\"",
            args);
    }
}
