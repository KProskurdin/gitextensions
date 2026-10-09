using System.ComponentModel.Composition;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.App.RepositoryHosts;
using GitExtensions.Xplat.Core.Plugins;
using GitExtensions.Xplat.Core.Repository;
using GitUI;
using GitUIPluginInterfaces.RepositoryHosts;
using NUnit.Framework;
using DialogResult = System.Windows.Forms.DialogResult;

namespace GitExtensions.Xplat.App.Tests;

// The repository host windows (upstream's RepoHosting forms) with a fake host plugin, and upstream's GitHub plugin loaded
// from the Plugins folder, without its network service.
internal sealed class RepositoryHostTests
{
    private const string Diff = "diff --git a/a.txt b/a.txt\nindex 1111111..2222222 100644\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-one\n+two\n"
        + "diff --git a/b.txt b/b.txt\nnew file mode 100644\n--- /dev/null\n+++ b/b.txt\n@@ -0,0 +1 @@\n+new\n";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private readonly List<string> _folders = [];
    private readonly List<string> _messages = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _messages.Clear();
        MessageBoxHost.Answer((_, text, _, _, _, _) =>
        {
            _messages.Add(text);
            return DialogResult.OK;
        });
    }

    [TearDown]
    public void TearDown()
    {
        MessageBoxHost.Answer(null);
        TestAppBuilder.UsePlugins();
        _repo.Dispose();
        foreach (string folder in _folders)
        {
            GitProcess.DeleteFolder(folder);
        }
    }

    [AvaloniaTest]
    public void Fork_and_clone_clones_the_users_repository_and_adds_its_parent_as_a_remote()
    {
        string remote = BareCopy();
        string parent = BareCopy();
        FakeRepositoryHost host = new();
        FakeHostedRepository mine = new("me", "project", remote) { ParentUrl = parent, ParentOwner = "team", Forks = 3 };
        host.MyRepos.Add(mine);
        MainWindow window = OpenWindow(host);
        Find<MenuItem>(window, "RepositoryHostsMenu").Header.Should().Be("FakeHub");

        Click(window, "ForkCloneRepositoryMenuItem");
        ForkAndCloneWindow fork = WaitForWindow<ForkAndCloneWindow>(window);
        ListBox myRepos = Find<ListBox>(fork, "myReposLV");
        WaitUntil(() => myRepos.Items.OfType<HostedRepositoryRow>().Any(row => row.Repo is not null));
        fork.Title.Should().Be("FakeHub: Remote repository fork and clone");
        myRepos.Items.OfType<HostedRepositoryRow>().Should().ContainSingle()
            .Which.Should().Be(new HostedRepositoryRow(mine, "project", "me", "Yes", "3", "No"));
        myRepos.SelectedIndex = 0;
        string destination = NewFolder();
        Find<TextBox>(fork, "destinationTB").Text = destination;

        Find<TextBox>(fork, "createDirTB").Text.Should().Be("project");
        Find<ComboBox>(fork, "addUpstreamRemoteAsCB").Text.Should().Be("team");
        Find<TextBlock>(fork, "cloneInfoText").Text.Should().Contain("You will have push access")
            .And.Contain("\"team\" will be added as a remote.");
        Click(fork, "cloneBtn");

        string target = Path.Combine(destination, "project");
        WaitUntil(() => !fork.IsVisible);
        GitProcess.Run(target, "remote", "get-url", "team").Trim().Replace('\\', '/').Should().Be(parent.Replace('\\', '/'));
        WaitUntil(() => Find<TextBox>(window, "PathBox").Text == target);
    }

    [AvaloniaTest]
    public void Fork_and_clone_searches_and_forks_with_the_host_plugin()
    {
        FakeRepositoryHost host = new();
        FakeHostedRepository other = new("ann", "tool", "https://example.com/ann/tool.git") { Description = "A tool" };
        host.SearchResults.Add(other);
        MainWindow window = OpenWindow(host);
        Click(window, "ForkCloneRepositoryMenuItem");
        ForkAndCloneWindow fork = WaitForWindow<ForkAndCloneWindow>(window);

        Find<TabControl>(fork, "tabControl").SelectedItem = Find<TabItem>(fork, "searchReposPage");
        Find<TextBox>(fork, "searchTB").Text = "tool";
        Click(fork, "searchBtn");
        ListBox results = Find<ListBox>(fork, "searchResultsLV");
        WaitUntil(() => results.Items.OfType<HostedRepositoryRow>().Any(row => row.Repo is not null));
        results.SelectedIndex = 0;
        Find<TextBlock>(fork, "searchResultItemDescription").Text.Should().Be("A tool");
        Find<TextBlock>(fork, "cloneInfoText").Text.Should().Contain("You can not push unless you are a collaborator");
        Click(fork, "forkBtn");

        WaitUntil(() => other.ForkCount == 1);
        fork.Close();
    }

    [AvaloniaTest]
    public void View_pull_requests_shows_the_files_and_discussion_posts_a_comment_and_fetches_the_pull_request()
    {
        string remote = BareCopy();
        _repo.Run("push", "-q", remote, "HEAD:refs/heads/feature");
        FakeRepositoryHost host = new();
        FakeHostedRepository project = new("team", "project", remote);
        FakePullRequest pullRequest = new(project, "feature", Diff)
        {
            Id = "7", Title = "Fix things", Owner = "ann", FetchBranch = "pr/n7_ann/feature",
        };
        pullRequest.Discussion.Entries.Add(new FakeDiscussionEntry("ann", "Please review"));
        pullRequest.Discussion.Entries.Add(new FakeCommitEntry("ann", "Fix things", "0123456789abcdef0123456789abcdef01234567"));
        project.PullRequests.Add(pullRequest);
        host.Remotes.Add(new FakeHostedRemote("origin", project, isOwnedByMe: false));
        MainWindow window = OpenWindow(host);

        Click(window, "ViewPullRequestsMenuItem");
        ViewPullRequestsWindow pulls = WaitForWindow<ViewPullRequestsWindow>(window);
        WaitUntil(() => pulls.PullRequests is [{ Info: not null }]);
        pulls.PullRequests[0].Should().BeEquivalentTo(new { Id = "7", Title = "Fix things", Owner = "ann", FetchBranch = "pr/n7_ann/feature" });
        ListBox files = Find<ListBox>(pulls, "_fileStatusList");
        ItemsControl discussion = Find<ItemsControl>(pulls, "_discussionWB");
        WaitUntil(() => files.ItemCount == 2 && discussion.ItemCount == 2);
        files.Items.Should().Equal("a.txt", "b.txt");
        discussion.Items.OfType<DiscussionEntryRow>().Select(entry => entry.Commit)
            .Should().Equal(null, "Commit:  0123456789abcdef0123456789abcdef01234567");

        Find<TextBox>(pulls, "_postCommentText").Text = "Thanks";
        Click(pulls, "_postComment");
        WaitUntil(() => discussion.ItemCount == 3);
        pullRequest.Discussion.Posted.Should().Equal("Thanks");

        Click(pulls, "_fetchBtn");
        WaitUntil(() => !pulls.IsVisible);
        _repo.Run("rev-parse", "--verify", "refs/heads/pr/n7_ann/feature").Trim().Should()
            .Be(_repo.Run("rev-parse", "HEAD").Trim());
    }

    [AvaloniaTest]
    public void Create_pull_request_sends_the_branches_the_suggested_title_and_the_template_body()
    {
        string remote = BareCopy();
        _repo.Run("push", "-q", remote, "HEAD:refs/heads/feature");
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("fetch", "-q", "origin");
        Directory.CreateDirectory(Path.Combine(_repo.Path, ".github"));
        File.WriteAllText(Path.Combine(_repo.Path, ".github", "PULL_REQUEST_TEMPLATE.md"), "## Checklist");
        FakeRepositoryHost host = new();
        FakeHostedRepository mine = new("me", "project", remote) { IsMine = true, DefaultBranch = "feature" };
        mine.Branches.Add("feature");
        FakeHostedRepository upstreamProject = new("team", "project", "https://example.com/team/project.git") { DefaultBranch = "dev" };
        upstreamProject.Branches.AddRange(["main", "dev"]);
        host.Remotes.Add(new FakeHostedRemote("origin", mine, isOwnedByMe: true));
        host.Remotes.Add(new FakeHostedRemote("upstream", upstreamProject, isOwnedByMe: false));
        MainWindow window = OpenWindow(host);

        Click(window, "CreatePullRequestsMenuItem");
        CreatePullRequestWindow create = WaitForWindow<CreatePullRequestWindow>(window);
        TextBox title = Find<TextBox>(create, "_titleTB");
        WaitUntil(() => create.AreBranchesLoaded && Find<ComboBox>(create, "_remoteBranchesCB").SelectedItem is "dev"
                        && title.Text == "second");
        Find<TextBox>(create, "_bodyTB").Text.Should().Be("## Checklist");
        Click(create, "_createBtn");

        WaitUntil(() => upstreamProject.CreatedPullRequests.Count == 1);
        upstreamProject.CreatedPullRequests[0].Should().Be(("feature", "dev", "second", "## Checklist"));
        WaitUntil(() => !create.IsVisible);
        _messages.Should().Equal("Done");
    }

    [AvaloniaTest]
    public void Add_upstream_remote_fetches_the_remote_the_host_plugin_added()
    {
        string remote = BareCopy();
        _repo.Run("push", "-q", remote, "HEAD:refs/heads/feature");
        _repo.Run("remote", "add", "upstream", remote);
        FakeRepositoryHost host = new() { UpstreamRemoteToAdd = "upstream" };
        host.Remotes.Add(new FakeHostedRemote("upstream", new FakeHostedRepository("team", "project", remote), isOwnedByMe: false));
        MainWindow window = OpenWindow(host);

        Click(window, "AddUpstreamRemoteMenuItem");

        WaitUntil(() => GitProcess.RunAllowingFailure(_repo.Path, "rev-parse", "--verify", "-q", "refs/remotes/upstream/feature") == 0);
    }

    [AvaloniaTest]
    public void The_github_plugin_adds_its_menu_blame_items_settings_links_and_commit_template()
    {
        Type type = PluginAssembly("GitHub3").GetTypes().Single(candidate =>
            candidate.GetCustomAttributes<ExportAttribute>().Any(export => export.ContractType == typeof(IGitPlugin)));
        IGitPlugin plugin = (IGitPlugin)Activator.CreateInstance(type)!;
        _repo.Run("remote", "add", "origin", "https://github.com/owner/repo.git");
        MainWindow window = OpenWindow(plugin);
        Find<MenuItem>(window, "RepositoryHostsMenu").Header.Should().Be("GitHub");

        // Upstream's blame menu item, built by the plugin itself.
        IReadOnlyList<MenuItem> blameItems = RepositoryHostMenus.ForBlame((IRepositoryHostPlugin)plugin,
            new GitBlameContext("a.txt", 0, 0, ObjectId.Parse("0123456789abcdef0123456789abcdef01234567")));
        blameItems.Should().ContainSingle();
        ((TextBlock)blameItems[0].Header!).Text.Should().Be("View in GitHub");
        blameItems[0].Items.Should().ContainSingle();

        // The token links are links, the restart note is text.
        PluginSettingsEditor settings = new(plugin, TestAppBuilder.PluginSettings);
        settings.Rows.Where(row => row.Kind == PluginSettingKind.Link).Select(row => row.Text).Should().Equal(
            "Generate a GitHub personal access token", "Manage GitHub personal access token");
        settings.Rows.Should().Contain(row => row.Kind == PluginSettingKind.Note);

        // Without a token, the plugin's commit template says so when the commit window opens.
        ((GitPluginSettingsContainer)plugin.SettingsContainer!).SetSettingsSource(TestAppBuilder.PluginSettings);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        Click(window, "RefreshButton");
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Click(window, "CommitDialogButton");
        CommitWindow commit = WaitForWindow<CommitWindow>(window);
        try
        {
            CommitTemplates.Registered().Select(template => template.Name).Should().Contain("No GitHub personal access token (PAT) defined");
        }
        finally
        {
            commit.Close();
            CommitTemplates.Unregister("No GitHub personal access token (PAT) defined");
        }
    }

    private static Assembly PluginAssembly(string name)
    {
        string assemblyName = "GitExtensions.Plugins." + name;
        return Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Plugins", assemblyName, assemblyName + ".dll"));
    }

    private string BareCopy()
    {
        string folder = Path.Combine(NewFolder(), "remote.git");
        GitProcess.Run(Path.GetDirectoryName(folder)!, "clone", "-q", "--bare", _repo.Path, folder);
        return folder;
    }

    private string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _folders.Add(folder);
        return folder;
    }

    private MainWindow OpenWindow(IGitPlugin plugin)
    {
        TestAppBuilder.UsePlugins(plugin);
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Find<TextBox>(window, "PathBox").Text = _repo.Path;
        Click(window, "OpenButton");
        WaitUntil(() => Find<Button>(window, "OpenButton").IsEnabled && Find<MenuItem>(window, "RepositoryHostsMenu").IsVisible
                        && Find<MenuItem>(window, "PluginsMenu").Items.OfType<MenuItem>().Any(item => item.Tag == plugin));
        return window;
    }

    private static TWindow WaitForWindow<TWindow>(Window owner)
        where TWindow : Window
    {
        WaitUntil(() => owner.OwnedWindows.OfType<TWindow>().Any(window => window.IsVisible));
        return owner.OwnedWindows.OfType<TWindow>().Single(window => window.IsVisible);
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        RoutedEvent click = control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent;
        control.RaiseEvent(new RoutedEventArgs(click));
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }
}
