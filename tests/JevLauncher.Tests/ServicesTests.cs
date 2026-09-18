using JevLauncher.Core;
using Xunit;

public class ServicesTests
{
    [Fact]
    public void Clipboard_keeps_newest_first_and_dedupes()
    {
        var history = new ClipboardHistory();
        history.Push("a");
        history.Push("b");
        history.Push("a");
        Assert.Equal(new[] { "a", "b" }, history.Items);
    }

    [Fact]
    public void Clipboard_ignores_blank_and_caps_the_list()
    {
        var history = new ClipboardHistory(capacity: 3);
        history.Push("   ");
        history.Push("1");
        history.Push("2");
        history.Push("3");
        history.Push("4");
        Assert.Equal(3, history.Items.Count);
        Assert.Equal("4", history.Items[0]);
    }

    [Fact]
    public void Notes_append_and_recent_strips_the_timestamp()
    {
        var path = Path.Combine(Path.GetTempPath(), "jev-notes-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var store = new NotesStore(path);
            store.Append("comprar cafe");
            store.Append("llamar a santi");

            var recent = store.Recent(5);
            Assert.Equal("llamar a santi", recent[0]);
            Assert.Equal("comprar cafe", recent[1]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Empty_notes_recent_is_empty()
    {
        var path = Path.Combine(Path.GetTempPath(), "jev-none-" + Guid.NewGuid().ToString("N") + ".txt");
        Assert.Empty(new NotesStore(path).Recent());
    }

    [Fact]
    public void Snippet_filter_matches_name_and_text()
    {
        var snippets = new[]
        {
            new Snippet("email", "santi@example.com"),
            new Snippet("firma", "Saludos, Santiago"),
        };

        Assert.Equal(2, SnippetCandidates.Build(snippets, string.Empty).Count);

        var filtered = SnippetCandidates.Build(snippets, "firma");
        Assert.Single(filtered);
        Assert.Equal(CandidateKind.Copy, filtered[0].Kind);
        Assert.Equal("Saludos, Santiago", filtered[0].Target);
    }
}
