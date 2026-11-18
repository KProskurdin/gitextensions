using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.RepositoryHosts;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The repository host windows' view models over substituted host plugins: no network, no git.
internal sealed class RepositoryHostViewModelTests
{
    private readonly List<(string Text, string Caption, bool IsError)> _messages = [];
    private FakeDialogHost _host = null!;

    [SetUp]
    public void SetUp()
    {
        _messages.Clear();
        _host = new FakeDialogHost();
    }

    [Test]
    public async Task Create_pull_request_should_close_with_upstreams_message_without_a_target()
    {
        IRepositoryHostPlugin plugin = Plugin(Remote("mine", Repository("me", "project"), mine: true));
        CreatePullRequestViewModel create = new(_host, Show, plugin, Module());

        await create.LoadAsync();

        _messages.Should().ContainSingle().Which.IsError.Should().BeTrue();
        _host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task Create_pull_request_should_suggest_the_title_and_send_the_chosen_branches()
    {
        IHostedRepository upstream = Repository("team", "project", "main", "dev");
        IHostedRepository mine = Repository("me", "project", "feature");
        IGitModule module = Module();
        module.GetPreviousCommitMessages(1, "mine/feature", string.Empty).Returns(["Add things\n\nbody"]);
        CreatePullRequestViewModel create = new(_host, Show,
            Plugin(Remote("origin", upstream, mine: false), Remote("mine", mine, mine: true)), module);

        await create.LoadAsync();
        await WaitUntilAsync(() => create.PullRequestTitle == "Add things" && create.SelectedTargetBranch == "main");

        create.CreateCommand.CanExecute(null).Should().BeTrue();
        create.TargetBranchText = "dev";
        await create.CreateCommand.ExecuteAsync(null);

        upstream.Received(1).CreatePullRequest("feature", "dev", "Add things", "");
        _messages.Should().ContainSingle().Which.IsError.Should().BeFalse();
        _host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task View_pull_requests_should_list_them_split_the_diff_and_show_the_discussion()
    {
        IPullRequestInformation pullRequest = Substitute.For<IPullRequestInformation>();
        pullRequest.Id.Returns("7");
        pullRequest.Title.Returns("Fix things");
        pullRequest.HeadRepo.Returns(Substitute.For<IHostedRepository>());
        pullRequest.GetDiffDataAsync().Returns(Task.FromResult(
            "diff --git a/a.txt b/a.txt\n--- a/a.txt\n+++ b/a.txt\n@@ -1 +1 @@\n-a\n+b\ndiff --git a/b.txt b/b.txt\n--- a/b.txt\n+++ b/b.txt\n"));
        IPullRequestDiscussion discussion = Substitute.For<IPullRequestDiscussion>();
        IDiscussionEntry comment = Substitute.For<IDiscussionEntry>();
        comment.Author.Returns("ann");
        comment.Body.Returns("Please review");
        discussion.Entries.Returns([comment]);
        pullRequest.GetDiscussion().Returns(discussion);
        IHostedRepository project = Repository("team", "project");
        project.GetPullRequests().Returns([pullRequest]);
        IGitModule module = Module();
        module.GetCurrentRemote().Returns("origin");
        module.GetRemotesAsync().Returns(Task.FromResult<IReadOnlyList<Remote>>([]));
        ViewPullRequestsViewModel pulls = new(_host, Show, Plugin(Remote("origin", project, mine: false)), module,
            new RepositoryOperationsViewModel(new FakeGitOperations()));
        pulls.FetchCommand.CanExecute(null).Should().BeFalse("no pull request is selected");

        await pulls.LoadAsync();
        await WaitUntilAsync(() => pulls.Files.Count == 2 && pulls.Discussion.Count == 1);

        pulls.PullRequests.Should().ContainSingle().Which.Id.Should().Be("7");
        pulls.Files.Should().Equal("a.txt", "b.txt");
        pulls.ShownPatch!.Name.Should().Be("a.txt");
        pulls.Discussion[0].Should().Be(new DiscussionEntryRow("ann", comment.Created.ToString(), null, "Please review"));
        pulls.FetchCommand.CanExecute(null).Should().BeTrue();

        pulls.SelectedFile = "b.txt";

        pulls.ShownPatch.Name.Should().Be("b.txt");
    }

    [Test]
    public async Task Fork_and_clone_should_fill_the_clone_options_from_the_selected_repository()
    {
        IHostedRepository project = Repository("me", "project");
        project.ParentOwner.Returns("team");
        project.CloneUrl.Returns("https://host/me/project.git");
        IRepositoryHostPlugin plugin = Plugin();
        plugin.GetMyRepos().Returns([project]);
        ForkAndCloneViewModel fork = new(_host, Show, plugin, new RepositoryOperationsViewModel(new FakeGitOperations()),
            "/work", new ForkAndCloneServices());

        await fork.LoadAsync();

        fork.MyRepos.Should().ContainSingle().Which.Should()
            .Be(new HostedRepositoryRow(project, "project", "me", "[t]No", "0", "[t]No"));
        fork.CloneCommand.CanExecute(null).Should().BeFalse();

        fork.SelectedMyRepo = fork.MyRepos[0];

        (fork.CreateDirectory, fork.UpstreamRemote, fork.CanAddUpstreamRemote).Should().Be(("project", "team", true));
        fork.UpstreamRemoteChoices.Should().Equal("team", "upstream");
        fork.CloneInfo.Should().Contain("https://host/me/project.git").And.Contain("\"team\" will be added as a remote.");
        fork.CloneCommand.CanExecute(null).Should().BeTrue();

        fork.Destination = "";
        fork.CloneInfo.Should().NotContain("/work");
    }

    [Test]
    public async Task Fork_and_clone_should_fork_a_search_result_and_list_the_users_repositories_again()
    {
        IHostedRepository found = Repository("ann", "tool");
        found.Description.Returns("A tool");
        IRepositoryHostPlugin plugin = Plugin();
        plugin.SearchForRepository("tool").Returns([found]);
        ForkAndCloneViewModel fork = new(_host, Show, plugin, new RepositoryOperationsViewModel(new FakeGitOperations()),
            "/work", new ForkAndCloneServices()) { SelectedTabIndex = ForkAndCloneViewModel.SearchTab, SearchText = "tool" };

        await fork.SearchCommand.ExecuteAsync(false);
        fork.SelectedSearchResult = fork.SearchResults.Single();

        (fork.Description, fork.ForkCommand.CanExecute(null)).Should().Be(("A tool", true));
        fork.CloneInfo.Should().Contain("You can not push unless you are a collaborator");

        await fork.ForkCommand.ExecuteAsync(null);

        found.Received(1).Fork();
        fork.SelectedTabIndex.Should().Be(ForkAndCloneViewModel.MyReposTab);
        plugin.Received(1).GetMyRepos();
    }

    private void Show(string text, string caption, bool isError) => _messages.Add((text, caption, isError));

    private static IGitModule Module()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns(Path.Combine(Path.GetTempPath(), "no-such-repository-" + Guid.NewGuid().ToString("N")));
        return module;
    }

    private static IRepositoryHostPlugin Plugin(params IHostedRemote[] remotes)
    {
        IRepositoryHostPlugin plugin = Substitute.For<IRepositoryHostPlugin>();
        plugin.Name.Returns("FakeHub");
        plugin.GetHostedRemotesForModule().Returns(remotes);
        return plugin;
    }

    private static IHostedRemote Remote(string name, IHostedRepository repository, bool mine)
    {
        IHostedRemote remote = Substitute.For<IHostedRemote>();
        remote.Name.Returns(name);
        remote.IsOwnedByMe.Returns(mine);
        remote.DisplayData.Returns(name);
        remote.GetHostedRepository().Returns(repository);
        return remote;
    }

    private static IHostedRepository Repository(string owner, string name, params string[] branches)
    {
        IHostedRepository repository = Substitute.For<IHostedRepository>();
        repository.Owner.Returns(owner);
        repository.Name.Returns(name);
        List<IHostedBranch> hostedBranches = [];
        foreach (string branch in branches)
        {
            IHostedBranch hosted = Substitute.For<IHostedBranch>();
            hosted.Name.Returns(branch);
            hostedBranches.Add(hosted);
        }

        repository.GetBranches().Returns(hostedBranches);
        repository.GetDefaultBranch().Returns(branches.FirstOrDefault() ?? "");
        repository.SupportedCloneProtocols.Returns(Array.Empty<GitProtocol>());
        return repository;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }
}
