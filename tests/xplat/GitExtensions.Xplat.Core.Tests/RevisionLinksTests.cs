using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Settings;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RevisionLinksTests
{
    private string _file = "";
    private FileRevisionLinkStore _store = null!;

    [SetUp]
    public void Setup()
    {
        _file = Path.Combine(Path.GetTempPath(), $"xplat-links-{Guid.NewGuid():N}.settings");
        _store = new FileRevisionLinkStore(_file);
    }

    [TearDown]
    public void TearDown() => File.Delete(_file);

    [Test]
    public void Add_should_start_from_upstreams_new_definition_and_Save_should_store_it_in_upstreams_setting()
    {
        RevisionLinkEditor editor = Open();

        RevisionLinkItem item = editor.Add();
        (item.Name, item.Enabled, item.SearchMessage, item.SearchLocalBranches, item.UseRemotesPattern,
                item.UseOnlyFirstRemote, item.SearchRemoteUrl)
            .Should().Be(("<new>", true, true, false, "upstream|origin", true, true));
        item.Name = "Issues";
        item.SearchPattern = "  #\\d+  ";
        item.NestedSearchPattern = "\\d+";
        item.AddFormat();
        RevisionLinkFormatItem format = item.AddFormat();
        format.Caption = "#{0}";
        format.Format = "https://example.com/issues/{0}";
        Save(editor);

        File.ReadAllText(_file).Should().Contain("RevisionLinkDefs").And.Contain("GitExtLinkDef");
        RevisionLinkItem stored = Open().Items.Single();
        (stored.Name, stored.SearchPattern, stored.NestedSearchPattern).Should().Be(("Issues", "#\\d+", "\\d+"));
        stored.Formats.Select(row => (row.Caption, row.Format)).Should()
            .Equal(("#{0}", "https://example.com/issues/{0}"));
    }

    [Test]
    public void Remove_should_drop_the_definition_from_the_stored_list()
    {
        RevisionLinkEditor editor = Open();
        editor.Add().Name = "a";
        editor.Add().Name = "b";
        Save(editor);

        RevisionLinkEditor again = Open();
        again.Remove(again.Items.Single(item => item.Name == "a"));
        Save(again);

        Open().Items.Select(item => item.Name).Should().Equal("b");
    }

    [Test]
    public void AddTemplates_should_use_upstreams_GitHub_template_for_the_preferred_remote()
    {
        RevisionLinkEditor editor = Open();
        ICloudProviderExternalLinkDefinitionExtractor gitHub =
            RevisionLinkTemplates.All.Single(template => template.ServiceName == "GitHub");

        IReadOnlyList<RevisionLinkItem> added = editor.AddTemplates(gitHub,
        [
            new Remote("origin", "https://github.com/me/repo.git", "https://github.com/me/repo.git"),
            new Remote("upstream", "https://github.com/owner/repo.git", "https://github.com/owner/repo.git"),
            new Remote("other", "https://example.com/x.git", "https://example.com/x.git"),
        ]);

        added.Select(item => item.Name).Should().Equal("GitHub - Code", "GitHub - Issues", "GitHub - Pull Requests");
        added[0].Formats.Select(row => row.Format).Should()
            .Equal("https://github.com/owner/repo/commit/%COMMIT_HASH%", "https://github.com/owner/repo");
        editor.AddTemplates(gitHub, []).Should().BeEmpty("a name already listed is not added again");
    }

    [Test]
    public void PreferredRemote_should_follow_upstreams_order()
    {
        Remote origin = new("origin", "o", "o");
        Remote fork = new("fork", "f", "f");
        Remote first = new("first", "x", "x");

        RevisionLinkTemplates.PreferredRemote([first, origin, fork]).Name.Should().Be("fork");
        RevisionLinkTemplates.PreferredRemote([first, origin]).Name.Should().Be("origin");
        RevisionLinkTemplates.PreferredRemote([first]).Name.Should().Be("first");
        RevisionLinkTemplates.PreferredRemote([]).Name.Should().BeNull();
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string pages = Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "CommandsDialogs", "SettingsDialog", "Pages");
        string page = File.ReadAllText(Path.Combine(pages, "RevisionLinksSettingsPage.cs"));
        string designer = File.ReadAllText(Path.Combine(pages, "RevisionLinksSettingsPage.Designer.cs"));

        RevisionLinkTemplates.All.Select(RevisionLinkTemplates.MenuText).Should()
            .Equal("Add GitHub templates", "Add Azure DevOps templates");
        page.Should().Contain("new(\"Add {0} templates\")").And.Contain("Name = \"<new>\"")
            .And.Contain("UseRemotesPattern = \"upstream|origin\"");
        foreach (string label in (string[])
                 [
                     "Categories", "Name", "Enabled", "Revision data", "Search in", "Message", "Local branch name",
                     "Remote branch name", "Search pattern", "Nested pattern", "Remote data", "Use remotes",
                     "Only use the first match", "URL", "Push URL", "Links", "Add", "Remove",
                 ])
        {
            designer.Should().Contain($"Text = \"{label}\";");
        }
    }

    // The editor writes into the settings it was opened with, which the store then saves, as the Settings window does.
    private readonly Dictionary<RevisionLinkEditor, DistributedSettings> _opened = [];

    private RevisionLinkEditor Open()
    {
        DistributedSettings settings = _store.Open(repositoryPath: null);
        RevisionLinkEditor editor = new(settings);
        _opened[editor] = settings;
        return editor;
    }

    private void Save(RevisionLinkEditor editor)
    {
        editor.Save();
        _store.Save(_opened[editor]);
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
