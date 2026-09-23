namespace JevLauncher.Core;

public static class UpdateApply
{
    public static string BuildCommandArguments(string setupPath, string exePath)
        => $"/c \"\"{setupPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART ; start \"\" \"{exePath}\"\"";
}
