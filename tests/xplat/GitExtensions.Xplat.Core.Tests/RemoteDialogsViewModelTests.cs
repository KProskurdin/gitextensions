using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RemoteDialogsViewModelTests
{
    private const string RepositoryPath = "C:/repo/";
    private const string OriginUrl = "https://example.com/origin.git";

    [Test]
    public void PushViewModel_should_show_the_remotes_URL_when_pushing_to_a_URL()
    {
        PushViewModel viewModel = NewPush(new FakeDialogHost());
        viewModel.Remote.Should().Be("origin");
        viewModel.Title.Should().Be($"{FakeDialogHost.TranslatedMark}Push ({RepositoryPath})");

        viewModel.ToUrl = true;

        viewModel.Destination.Should().Be(OriginUrl);
        viewModel.Dialog.ToUrl.Should().BeTrue();
    }

    [Test]
    public async Task PushViewModel_should_close_with_upstreams_request_for_a_tag()
    {
        FakeDialogHost host = new();
        PushViewModel viewModel = NewPush(host);
        viewModel.SelectedTab = 1;
        viewModel.Tag = "v1";
        viewModel.ForceWithLease = true;
        viewModel.Force.Should().BeFalse("force with lease and force exclude each other");

        await viewModel.PushCommand.ExecuteAsync(null);

        host.Result.Should().Be(new PushRequest("origin", "", "", ForcePushOptions.Force, false, PushKind.Tag, "v1"));
    }

    [Test]
    public async Task PullViewModel_should_switch_to_fetch_and_close_with_upstreams_request()
    {
        FakeDialogHost host = new();
        InMemoryAppPreferences preferences = new();
        PullViewModel viewModel = NewPull(host, preferences, rebasesMergeCommit: false);
        viewModel.FetchOptionsEnabled.Should().BeFalse();

        viewModel.Action = PullAction.FetchOnly;

        viewModel.FetchOptionsEnabled.Should().BeTrue();
        viewModel.Title.Should().Be($"{FakeDialogHost.TranslatedMark}Fetch ({RepositoryPath})");
        viewModel.PullButtonText.Should().Be($"{FakeDialogHost.TranslatedMark}_Fetch");

        viewModel.Action = PullAction.Merge;
        viewModel.Tags = PullTags.None;
        viewModel.AutoStash = true;
        await viewModel.PullCommand.ExecuteAsync(null);

        host.Result.Should().Be(new PullRequest("origin", "", PullAction.Merge, Prune: false, AutoStash: true,
            LocalBranch: "", FetchTags: false));
        preferences.AutoStash.Should().BeTrue();
    }

    [Test]
    public async Task PullViewModel_should_close_without_a_request_when_the_rebase_question_is_cancelled()
    {
        FakeDialogHost host = new() { ThreeWayAnswer = null };
        PullViewModel viewModel = NewPull(host, new InMemoryAppPreferences(), rebasesMergeCommit: true);
        viewModel.Action = PullAction.Rebase;

        await viewModel.PullCommand.ExecuteAsync(null);

        host.Questions.Should().ContainSingle();
        host.IsClosed.Should().BeTrue();
        host.Result.Should().BeNull();
    }

    [Test]
    public void CommandLogViewModel_should_show_the_cache_and_store_the_call_stack_choice()
    {
        CommandCache cache = new();
        bool captured = false;
        CommandLogViewModel viewModel = new(new FakeDialogHost(), cache, captureCallStacks: false,
            value => captured = value, action => action());
        viewModel.Attach();
        try
        {
            cache.Add("-c color.ui=never rev-parse HEAD", "abc\n", "");
            viewModel.SelectedTab = CommandLogViewModel.CacheTab;
            viewModel.CacheRows.Should().Equal("rev-parse HEAD");
            viewModel.CacheDetail.Should().Be(CommandLogDialog.CacheDetail("-c color.ui=never rev-parse HEAD", "abc\n", ""));

            viewModel.ClearCacheCommand.Execute(null);
            viewModel.CachedCommands.Should().BeEmpty();
            viewModel.CaptureCallStacks = true;
            captured.Should().BeTrue();
        }
        finally
        {
            viewModel.Detach();
        }
    }

    private static PushViewModel NewPush(FakeDialogHost host)
        => new(host,
            new PushDialog(new PushRepository("main", [new ConfigFileRemote { Name = "origin", Url = OriginUrl }], [],
                [], ["v1"], "origin", null, IsBare: false, AutoSetupMerge: null, (_, _) => null)),
            new InMemoryAppPreferences(), Substitute.For<IRemoteUrlHistory>(), RepositoryPath, canCreatePullRequest: false,
            openPull: null, manageRemotes: null, loadSshKey: null, readRemoteHeads: null,
            confirmNewBranch: () => Task.FromResult(true));

    private static PullViewModel NewPull(FakeDialogHost host, IAppPreferences preferences, bool rebasesMergeCommit)
        => new(host,
            new PullDialog(["origin", "upstream"], "main", GitPullAction.None, GitPullAction.Merge, null, "origin"),
            preferences, Substitute.For<IRemoteUrlHistory>(), RepositoryPath, ["origin/main"], ["main"],
            isShallow: false,
            new PullViewModelGit(_ => Task.FromResult<string?>("origin"), (_, _, _) => Task.FromResult(rebasesMergeCommit),
                () => Task.FromResult(true), (_, _) => Task.FromResult(true)));
}
