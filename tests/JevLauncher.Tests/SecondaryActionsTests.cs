using JevLauncher.Core;
using Xunit;

public class SecondaryActionsTests
{
    private static Candidate File_() =>
        new("file", CandidateKind.OpenFile, "budget.pdf", "PDF", "budget.pdf", typeof(SecondaryActionsTests).Assembly.Location);

    private static Candidate Url() =>
        new("url", CandidateKind.OpenUrl, "TypeSafe", "typesafe.ai", "typesafe", "https://typesafe.ai/");

    private static Candidate StoreApp() =>
        new("store", CandidateKind.OpenApp, "Calculadora", "Application", "calculadora",
            @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

    [Fact]
    public void Files_offer_copy_path_folder_and_reveal()
    {
        var ids = SecondaryActions.For(File_()).Select(a => a.Id).ToList();

        Assert.Contains("act:open", ids);
        Assert.Contains("act:copy", ids);
        Assert.Contains("act:folder", ids);
        Assert.Contains("act:reveal", ids);
        Assert.Contains("act:web", ids);
    }

    [Fact]
    public void Urls_offer_copy_url_but_no_folder_actions()
    {
        var ids = SecondaryActions.For(Url()).Select(a => a.Id).ToList();

        Assert.Contains("act:copy", ids);
        Assert.DoesNotContain("act:folder", ids);
        Assert.DoesNotContain("act:reveal", ids);
    }

    [Fact]
    public void Store_apps_only_offer_open_and_web()
    {
        var ids = SecondaryActions.For(StoreApp()).Select(a => a.Id).ToList();

        Assert.Equal(new[] { "act:open", "act:web" }, ids);
    }

    [Fact]
    public void Copy_action_carries_the_target_as_payload()
    {
        var copy = SecondaryActions.For(Url()).First(a => a.Id == "act:copy");
        Assert.Equal("https://typesafe.ai/", copy.Target);
    }
}
