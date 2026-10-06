using Avalonia.Headless.NUnit;
using GitCommands.Git;
using GitCommands.Logging;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Scripts;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Real git writes need ThreadHelper.JoinableTaskContext, which the app sets at startup; AvaloniaTest runs the app.
internal sealed class GitOperationsTests
{
    private TestRepository _repo = null!;
    private GitOperations _operations = null!;
    private readonly List<string> _folders = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _operations = new GitOperations();
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
        foreach (string folder in _folders)
        {
            GitProcess.DeleteFolder(folder);
        }
    }

    [AvaloniaTest]
    public void Stage_and_commit_add_a_commit_with_the_message()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        Wait(_operations.StageAsync(_repo.Path, ["new.txt"]));
        Wait(_operations.CommitAsync(_repo.Path, "add new", amend: false, signOff: false, author: ""));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("add new");
        _repo.Run("rev-list", "--count", "HEAD").Trim().Should().Be("3");
    }

    [AvaloniaTest]
    public void Commit_with_an_author_records_that_author()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        Wait(_operations.StageAsync(_repo.Path, ["new.txt"]));

        Wait(_operations.CommitAsync(_repo.Path, "by someone", amend: false, signOff: false,
            author: "Other Person <other@example.com>"));

        _repo.Run("log", "-1", "--format=%an <%ae>").Trim().Should().Be("Other Person <other@example.com>");
    }

    [AvaloniaTest]
    public void Commit_with_sign_off_adds_the_signed_off_by_trailer()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        Wait(_operations.StageAsync(_repo.Path, ["new.txt"]));

        Wait(_operations.CommitAsync(_repo.Path, "signed", amend: false, signOff: true, author: ""));

        _repo.Run("log", "-1", "--format=%B").Should().Contain("Signed-off-by: Test <test@example.com>");
    }

    [AvaloniaTest]
    public void GetHeadMessageAsync_returns_the_full_message_of_the_last_commit()
    {
        new GitRepositoryService().GetHeadMessageAsync(_repo.Path).GetAwaiter().GetResult()
            .Should().Be("second\n\nbody line");
    }

    [AvaloniaTest]
    public void Commit_with_amend_rewrites_the_last_commit_message()
    {
        Wait(_operations.CommitAsync(_repo.Path, "reworded", amend: true, signOff: false, author: ""));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("reworded");
        _repo.Run("rev-list", "--count", "HEAD").Trim().Should().Be("2");
    }

    [AvaloniaTest]
    public void Commit_without_staged_changes_throws_with_git_message()
    {
        Func<Task> commit = () =>
            _operations.CommitAsync(_repo.Path, "nothing", amend: false, signOff: false, author: "");

        Action act = () => Wait(commit());
        act.Should().Throw<GitOperationException>();
    }

    [AvaloniaTest]
    public void Unstage_takes_a_staged_file_out_of_the_index()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        Wait(_operations.StageAsync(_repo.Path, ["new.txt"]));

        Wait(_operations.UnstageAsync(_repo.Path, ["new.txt"]));

        _repo.Run("diff", "--cached", "--name-only").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void CreateBranch_with_checkout_switches_to_the_new_branch()
    {
        Wait(_operations.CreateBranchAsync(_repo.Path, "feature/x", checkout: true));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature/x");
    }

    [AvaloniaTest]
    public void CreateBranch_at_a_start_point_points_the_branch_at_that_commit()
    {
        Wait(_operations.CreateBranchAsync(_repo.Path, "from-first", checkout: false, startPoint: "HEAD~1"));

        _repo.Run("rev-parse", "from-first").Trim().Should().Be(_repo.Run("rev-parse", "HEAD~1").Trim());
    }

    [AvaloniaTest]
    public void Checkout_returns_to_an_existing_branch()
    {
        string original = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.CreateBranchAsync(_repo.Path, "feature/x", checkout: true));

        Wait(_operations.CheckoutAsync(_repo.Path, original));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be(original);
    }

    [AvaloniaTest]
    public void AddRemote_and_RemoveRemote_change_the_remotes_of_the_snapshot()
    {
        string remote = CreateBareRemote();

        Wait(_operations.AddRemoteAsync(_repo.Path, "upstream", remote));
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).Remotes.Should().Equal("upstream");

        Wait(_operations.RemoveRemoteAsync(_repo.Path, "upstream"));
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).Remotes.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void RenameBranch_renames_a_branch_that_is_not_checked_out()
    {
        Wait(_operations.CreateBranchAsync(_repo.Path, "old", checkout: false));

        Wait(_operations.RenameBranchAsync(_repo.Path, "old", "new"));

        _repo.Run("branch", "--list", "old").Trim().Should().BeEmpty();
        _repo.Run("branch", "--list", "new").Trim().Should().Be("new");
    }

    [AvaloniaTest]
    public void RenameBranch_renames_the_checked_out_branch()
    {
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        Wait(_operations.RenameBranchAsync(_repo.Path, current, "renamed"));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("renamed");
    }

    [AvaloniaTest]
    public void DeleteBranch_removes_a_branch_that_is_not_checked_out()
    {
        Wait(_operations.CreateBranchAsync(_repo.Path, "feature/x", checkout: false));

        Wait(_operations.DeleteBranchAsync(_repo.Path, "feature/x", force: false));

        _repo.Run("branch", "--list", "feature/x").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void DeleteBranch_with_force_removes_a_branch_with_unmerged_commits()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "unmerged");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "work");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "unmerged work");
        _repo.Run("checkout", "-q", baseBranch);

        Action plain = () => Wait(_operations.DeleteBranchAsync(_repo.Path, "unmerged", force: false));
        plain.Should().Throw<GitOperationException>();

        Wait(_operations.DeleteBranchAsync(_repo.Path, "unmerged", force: true));

        _repo.Run("branch", "--list", "unmerged").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void DeleteBranch_of_the_checked_out_branch_throws()
    {
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        Func<Task> delete = () => _operations.DeleteBranchAsync(_repo.Path, current, force: false);

        Action act = () => Wait(delete());
        act.Should().Throw<GitOperationException>();
    }

    [AvaloniaTest]
    public void Push_publishes_the_current_branch_to_the_remote()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        Wait(_operations.PushAsync(_repo.Path, "origin", current));

        GitProcess.Run(remote, "rev-parse", current).Trim().Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
    }

    [AvaloniaTest]
    public void Init_creates_a_repository_in_a_new_folder()
    {
        string folder = Path.Combine(NewFolder(), "fresh");

        Wait(_operations.InitAsync(folder));

        Directory.Exists(Path.Combine(folder, ".git")).Should().BeTrue();
    }

    [AvaloniaTest]
    public void Clone_copies_the_remote_history()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        Wait(_operations.PushAsync(_repo.Path, "origin", _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim()));
        string target = NewFolder();

        Wait(_operations.CloneAsync(remote, target));

        GitProcess.Run(target, "log", "-1", "--format=%s").Trim().Should().Be("second");
    }

    [AvaloniaTest]
    public void Fetch_with_prune_removes_remote_tracking_branches_deleted_on_the_remote()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("branch", "gone");
        Wait(_operations.PushAsync(_repo.Path, "origin", "gone"));
        GitProcess.Run(remote, "branch", "-D", "gone");

        Wait(_operations.FetchAsync(_repo.Path, "origin", prune: true));

        _repo.Run("branch", "-r").Should().NotContain("origin/gone");
    }

    [AvaloniaTest]
    public void PushTags_publishes_the_local_tags_to_the_remote()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("tag", "v1.0");

        Wait(_operations.PushTagsAsync(_repo.Path, "origin"));

        GitProcess.Run(remote, "tag", "--list").Trim().Should().Be("v1.0");
    }

    [AvaloniaTest]
    public void Push_with_an_output_reports_git_messages_while_it_runs()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        List<GitOutputLine> output = [];

        Wait(_operations.PushAsync(_repo.Path, "origin", current, new CollectingProgress(output)));

        GitProcess.Run(remote, "rev-parse", current).Trim().Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        output.Should().Contain(line => line.Text.Contains(current, StringComparison.Ordinal) && !line.IsProgress);
        CommandLog.Commands.Should().Contain(entry => entry.Arguments.Contains("push", StringComparison.Ordinal)
                                                      && entry.Arguments.Contains("--progress",
                                                          StringComparison.Ordinal));
    }

    [AvaloniaTest]
    public void A_failing_fetch_with_an_output_throws_with_the_git_message()
    {
        _repo.Run("remote", "add", "origin", Path.Combine(NewFolder(), "missing"));
        List<GitOutputLine> output = [];

        Action act = () =>
            Wait(_operations.FetchAsync(_repo.Path, "origin", prune: false, new CollectingProgress(output)));

        act.Should().Throw<GitOperationException>().Which.Message.Should().NotBeEmpty();
        output.Should().NotBeEmpty();
    }

    [AvaloniaTest]
    public void A_cancelled_clone_with_an_output_throws_cancelled()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        Action act = () => Wait(_operations.CloneAsync(CreateBareRemote(), Path.Combine(NewFolder(), "clone"),
            new CollectingProgress([]), cancelled.Token));

        act.Should().Throw<OperationCanceledException>();
    }

    [AvaloniaTest]
    public void Push_with_lease_replaces_a_rewritten_branch_that_a_plain_push_refuses()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.PushAsync(_repo.Path, "origin", current));
        _repo.Run("commit", "-q", "--amend", "-m", "second, reworded");

        Action plain = () => Wait(_operations.PushAsync(_repo.Path,
            new PushRequest("origin", current, "", ForcePushOptions.DoNotForce, Track: false)));
        plain.Should().Throw<GitOperationException>();
        Wait(_operations.PushAsync(_repo.Path,
            new PushRequest("origin", current, "", ForcePushOptions.ForceWithLease, Track: false)));

        GitProcess.Run(remote, "log", "-1", "--format=%s", current).Trim().Should().Be("second, reworded");
    }

    [AvaloniaTest]
    public void Push_to_another_remote_branch_name_with_tracking_sets_the_upstream()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        Wait(_operations.PushAsync(_repo.Path,
            new PushRequest("origin", current, "published", ForcePushOptions.DoNotForce, Track: true)));

        GitProcess.Run(remote, "rev-parse", "published").Trim().Should().Be(_repo.Run("rev-parse", "HEAD").Trim());
        _repo.Run("rev-parse", "--abbrev-ref", "@{u}").Trim().Should().Be("origin/published");
    }

    [AvaloniaTest]
    public void Pull_request_fetch_only_updates_remote_tracking_branches_and_not_the_branch()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.PushAsync(_repo.Path, "origin", current));
        string target = NewFolder();
        Wait(_operations.CloneAsync(remote, target));
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "three");
        _repo.Run("commit", "-q", "-am", "third");
        Wait(_operations.PushAsync(_repo.Path, "origin", current));

        Wait(_operations.PullAsync(target,
            new PullRequest("origin", "", PullAction.FetchOnly, Prune: true, AutoStash: false)));

        GitProcess.Run(target, "log", "-1", "--format=%s").Trim().Should().Be("second");
        GitProcess.Run(target, "log", "-1", "--format=%s", $"origin/{current}").Trim().Should().Be("third");
    }

    [AvaloniaTest]
    public void Pull_request_with_auto_stash_rebases_and_keeps_local_changes()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.PushAsync(_repo.Path, "origin", current));
        string target = NewFolder();
        Wait(_operations.CloneAsync(remote, target));
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "from origin");
        _repo.Run("add", "new.txt");
        _repo.Run("commit", "-q", "-m", "third");
        Wait(_operations.PushAsync(_repo.Path, "origin", current));
        File.WriteAllText(Path.Combine(target, "a.txt"), "local edit");

        Wait(_operations.PullAsync(target,
            new PullRequest("origin", current, PullAction.Rebase, Prune: false, AutoStash: true)));

        GitProcess.Run(target, "log", "-1", "--format=%s").Trim().Should().Be("third");
        File.ReadAllText(Path.Combine(target, "a.txt")).Should().Be("local edit");
    }

    [AvaloniaTest]
    public void Update_submodules_initializes_a_submodule_listed_in_the_snapshot()
    {
        // git refuses local-path submodules unless the file protocol is allowed; this is set for this test's processes only.
        Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", "1");
        Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", "protocol.file.allow");
        Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", "always");
        try
        {
            string library = CreateBareRemote();
            _repo.Run("push", "-q", library, "HEAD:refs/heads/main");
            GitProcess.Run(library, "symbolic-ref", "HEAD", "refs/heads/main");
            _repo.Run("submodule", "add", "-q", library, "lib");
            _repo.Run("commit", "-q", "-m", "add submodule");
            _repo.Run("submodule", "deinit", "-q", "-f", "lib");

            RepositorySnapshot before = Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path));
            before.Submodules.Should().ContainSingle().Which.Should()
                .Be(new SubmoduleInfo("lib", IsInitialized: false, IsUpToDate: false));

            Wait(_operations.UpdateSubmodulesAsync(_repo.Path, path: null));

            File.Exists(Path.Combine(_repo.Path, "lib", "a.txt")).Should().BeTrue();
            Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).Submodules.Should().ContainSingle()
                .Which.IsInitialized.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CONFIG_COUNT", null);
            Environment.SetEnvironmentVariable("GIT_CONFIG_KEY_0", null);
            Environment.SetEnvironmentVariable("GIT_CONFIG_VALUE_0", null);
        }
    }

    [AvaloniaTest]
    public void Pull_fast_forwards_to_the_remote_commit()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        string current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        Wait(_operations.PushAsync(_repo.Path, "origin", current));
        string target = NewFolder();
        Wait(_operations.CloneAsync(remote, target));

        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "three");
        _repo.Run("commit", "-q", "-am", "third");
        Wait(_operations.PushAsync(_repo.Path, "origin", current));

        Wait(_operations.PullAsync(target, "origin", current, rebase: false));

        GitProcess.Run(target, "log", "-1", "--format=%s").Trim().Should().Be("third");
    }

    [AvaloniaTest]
    public void CheckoutRemote_creates_a_tracking_branch_with_the_remote_name()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("branch", "feature");
        Wait(_operations.PushAsync(_repo.Path, "origin", "feature"));
        _repo.Run("checkout", "-q", "--detach");
        _repo.Run("branch", "-D", "feature");

        Wait(_operations.CheckoutRemoteAsync(_repo.Path, "origin/feature"));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature");
        _repo.Run("config", "branch.feature.remote").Trim().Should().Be("origin");
    }

    [AvaloniaTest]
    public void Stash_saves_the_working_changes_with_the_message_and_pop_restores_them()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");

        Wait(_operations.StashAsync(_repo.Path, "work in progress", includeUntracked: false, keepIndex: false));

        _repo.Run("status", "--porcelain").Trim().Should().BeEmpty();
        _repo.Run("stash", "list").Should().Contain("work in progress");

        Wait(_operations.PopStashAsync(_repo.Path, "stash@{0}"));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("changed");
        _repo.Run("stash", "list").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Stash_with_include_untracked_also_stashes_new_files()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        Wait(_operations.StashAsync(_repo.Path, "with new file", includeUntracked: true, keepIndex: false));

        _repo.Run("status", "--porcelain").Trim().Should().BeEmpty();
        _repo.Run("stash", "list").Should().Contain("with new file");
    }

    [AvaloniaTest]
    public void DeleteUntracked_removes_only_the_named_files()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        File.WriteAllText(Path.Combine(_repo.Path, "other.txt"), "content");

        Wait(_operations.DeleteUntrackedAsync(_repo.Path, ["new.txt"]));

        File.Exists(Path.Combine(_repo.Path, "new.txt")).Should().BeFalse();
        File.Exists(Path.Combine(_repo.Path, "other.txt")).Should().BeTrue();
    }

    [AvaloniaTest]
    public void Discard_restores_the_tracked_file_from_the_index()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");

        Wait(_operations.DiscardChangesAsync(_repo.Path, ["a.txt"]));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("two");
        _repo.Run("status", "--porcelain").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Stash_with_keep_index_leaves_the_staged_changes_in_the_index()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");
        _repo.Run("add", "new.txt");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");

        Wait(_operations.StashAsync(_repo.Path, "keep staged", includeUntracked: false, keepIndex: true));

        _repo.Run("diff", "--cached", "--name-only").Trim().Should().Be("new.txt");
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("two");
    }

    [AvaloniaTest]
    public void Stash_of_selected_paths_leaves_the_other_changes_in_place()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "content");

        Wait(_operations.StashAsync(_repo.Path, "only a", includeUntracked: false, keepIndex: false, paths: ["a.txt"]));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("two");
        _repo.Run("status", "--porcelain").Trim().Should().Be("?? new.txt");
    }

    [AvaloniaTest]
    public void Apply_and_drop_act_on_the_named_stash_only()
    {
        SaveStash("one");
        SaveStash("three");

        Wait(_operations.ApplyStashAsync(_repo.Path, "stash@{1}"));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("one");
        _repo.Run("stash", "list").Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(2);

        Wait(_operations.DropStashAsync(_repo.Path, "stash@{1}"));

        _repo.Run("stash", "list").Should().Contain("saved three").And.NotContain("saved one");
    }

    [AvaloniaTest]
    public void Pop_of_an_older_stash_keeps_the_newer_one()
    {
        SaveStash("one");
        SaveStash("three");

        Wait(_operations.PopStashAsync(_repo.Path, "stash@{1}"));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("one");
        _repo.Run("stash", "list").Should().Contain("saved three").And.NotContain("saved one");
    }

    [AvaloniaTest]
    public void CreateTag_and_DeleteTag_add_and_remove_a_lightweight_tag()
    {
        Wait(_operations.CreateTagAsync(_repo.Path, "v1.0", "HEAD~1", ""));

        _repo.Run("tag", "--list").Trim().Should().Be("v1.0");
        GitProcess.Run(_repo.Path, "rev-parse", "v1.0").Trim().Should().Be(_repo.Run("rev-parse", "HEAD~1").Trim());

        Wait(_operations.DeleteTagAsync(_repo.Path, "v1.0"));

        _repo.Run("tag", "--list").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void CreateTag_with_a_message_makes_an_annotated_tag()
    {
        Wait(_operations.CreateTagAsync(_repo.Path, "v2.0", "HEAD", "release notes"));

        _repo.Run("cat-file", "-t", "v2.0").Trim().Should().Be("tag");
    }

    [AvaloniaTest]
    public void DeleteRemoteBranch_removes_the_branch_from_the_remote()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("branch", "feature");
        Wait(_operations.PushAsync(_repo.Path, "origin", "feature"));

        Wait(_operations.DeleteRemoteBranchAsync(_repo.Path, "origin", "feature"));

        GitProcess.Run(remote, "branch", "--list", "feature").Trim().Should().BeEmpty();
        _repo.Run("branch", "--list", "feature").Trim().Should().Be("feature");
    }

    [AvaloniaTest]
    public void CherryPick_applies_the_commit_to_the_current_branch()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "feature");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "feature work");
        string picked = _repo.Run("rev-parse", "HEAD").Trim();
        _repo.Run("checkout", "-q", baseBranch);

        Wait(_operations.CherryPickAsync(_repo.Path, picked));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("feature work");
        File.Exists(Path.Combine(_repo.Path, "f.txt")).Should().BeTrue();
    }

    [AvaloniaTest]
    public void Revert_adds_a_commit_that_undoes_the_selected_one()
    {
        Wait(_operations.RevertAsync(_repo.Path, "HEAD"));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("one");
        _repo.Run("log", "-1", "--format=%s").Should().StartWith("Revert \"second");
    }

    [AvaloniaTest]
    public void Rebase_replays_the_branch_onto_the_base()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "feature");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "feature work");

        Wait(_operations.RebaseAsync(_repo.Path, baseBranch));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("feature work");
        _repo.Run("rev-parse", "HEAD~1").Trim().Should().Be(_repo.Run("rev-parse", baseBranch).Trim());
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Conflicting_rebase_can_be_aborted()
    {
        string baseBranch = CreateRebaseConflict();
        Action rebase = () => Wait(_operations.RebaseAsync(_repo.Path, baseBranch));
        rebase.Should().Throw<GitOperationException>();
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeTrue();

        Wait(_operations.AbortRebaseAsync(_repo.Path));

        _repo.Run("status", "--porcelain").Trim().Should().BeEmpty();
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("feature");
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Conflicting_rebase_is_completed_by_staging_the_resolution_and_continuing()
    {
        string baseBranch = CreateRebaseConflict();
        Action rebase = () => Wait(_operations.RebaseAsync(_repo.Path, baseBranch));
        rebase.Should().Throw<GitOperationException>();

        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "resolved");
        Wait(_operations.StageAsync(_repo.Path, ["a.txt"]));
        Wait(_operations.ContinueRebaseAsync(_repo.Path));

        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature");
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("feature edit");
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("resolved");
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Interactive_rebase_applies_the_todo_list_the_editor_saved()
    {
        string editor = WriteEditorScript("""sed -e 's/^pick \(.* second\)$/drop \1/' "$1" > "$1.tmp" """);

        Wait(_operations.RebaseInteractiveAsync(_repo.Path, "HEAD~1", editor));

        _repo.Run("log", "--format=%s").Trim().Should().Be("first");
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Interactive_rebase_opens_the_same_editor_for_a_reworded_message()
    {
        string editor = WriteEditorScript("""
                                          case "$1" in
                                            *git-rebase-todo) sed -e 's/^pick /reword /' "$1" > "$1.tmp" ;;
                                            *) printf 'reworded\n' > "$1.tmp" ;;
                                          esac
                                          """);

        Wait(_operations.RebaseInteractiveAsync(_repo.Path, "HEAD~1", editor));

        _repo.Run("log", "-1", "--format=%B").Trim().Should().Be("reworded");
    }

    [AvaloniaTest]
    public void Interactive_rebase_stops_when_the_editor_fails()
    {
        string head = _repo.Run("rev-parse", "HEAD").Trim();
        string editor = WriteEditorScript("exit 1");

        Action rebase = () => Wait(_operations.RebaseInteractiveAsync(_repo.Path, "HEAD~1", editor));

        rebase.Should().Throw<GitOperationException>();
        _repo.Run("rev-parse", "HEAD").Trim().Should().Be(head);
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeFalse();
    }

    // A stand-in ssh (core.sshCommand) asks SSH_ASKPASS for a password as ssh does, reports what it got and fails, so the
    // test sees that a remote operation hands its prompts to the askpass program the operations were given.
    [AvaloniaTest]
    public void Remote_operations_let_ssh_ask_the_askpass_program()
    {
        string folder = NewFolder();
        string askPass = Path.Combine(folder, "askpass.sh");
        File.WriteAllText(askPass, "#!/bin/sh\necho \"answer to $1\"\n");
        string fakeSsh = Path.Combine(folder, "ssh.sh");
        File.WriteAllText(fakeSsh,
            "#!/bin/sh\nanswer=$(\"$SSH_ASKPASS\" \"secret\")\necho \"got <$answer> marker <$GITEXTENSIONS_XPLAT_ASKPASS>\" >&2\nexit 1\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(askPass, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        _repo.Run("config", "core.sshCommand", $"sh '{fakeSsh.Replace('\\', '/')}'");
        _repo.Run("remote", "add", "origin", "ssh://example.invalid/repo.git");
        GitOperations operations = new(askPassExecutable: askPass);

        Action fetch = () =>
            Wait(operations.FetchAsync(_repo.Path, "origin", prune: false, new CollectingProgress([])));

        fetch.Should().Throw<GitOperationException>().Which.Message.Should()
            .Contain("got <answer to secret> marker <1>");
    }

    [AvaloniaTest]
    public void Script_variables_are_read_from_the_repository()
    {
        string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        string first = _repo.Run("rev-parse", "HEAD~1").Trim();
        _repo.Run("tag", "v1", first);
        _repo.Run("remote", "add", "origin", "git@github.com:owner/repo.git");
        _repo.Run("config", $"branch.{branch}.remote", "origin");
        RepositoryScriptContext context = new(_repo.Path, [first], _ => Task.FromResult(""));

        string? arguments = Wait(Task.Run(() => ScriptVariables.ExpandAsync(
            "{sHash} {sTag} {sSubject} {cBranch} {cDefaultRemotePathFromUrl} {HEAD} {cSubject}", context)));

        arguments.Should().Be($"{first} v1 first {branch} /owner/repo {branch} second");
        context.RepoName.Should().Be(Path.GetFileName(_repo.Path));
    }

    // What git finds differs per machine; the list must come back and must not offer the tools that need a terminal.
    [AvaloniaTest]
    public void The_tool_catalog_lists_what_git_finds_without_terminal_tools()
    {
        GitDiffMergeToolCatalog catalog = new();

        IReadOnlyList<string> diffTools = Wait(catalog.GetAvailableAsync(diff: true));
        IReadOnlyList<string> mergeTools = Wait(catalog.GetAvailableAsync(diff: false));

        diffTools.Concat(mergeTools).Should().NotContain(tool => tool.StartsWith("vimdiff", StringComparison.Ordinal));
    }

    [AvaloniaTest]
    public void Skipping_the_conflicting_commit_completes_the_rebase_without_it()
    {
        string baseBranch = CreateRebaseConflict();
        Action rebase = () => Wait(_operations.RebaseAsync(_repo.Path, baseBranch));
        rebase.Should().Throw<GitOperationException>();

        Wait(_operations.SkipRebaseAsync(_repo.Path));

        _repo.Run("rev-parse", "HEAD").Trim().Should().Be(_repo.Run("rev-parse", baseBranch).Trim());
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsRebasing.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Resolving_a_conflict_with_theirs_stages_the_incoming_version()
    {
        StartConflictingMerge();

        Wait(_operations.ResolveConflictsAsync(_repo.Path, ["a.txt"], ours: false));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("feature");
        _repo.Run("diff", "--cached", "--name-only").Trim().Should().Be("a.txt");
    }

    [AvaloniaTest]
    public void Resolving_a_conflict_with_ours_keeps_the_current_version()
    {
        StartConflictingMerge();

        Wait(_operations.ResolveConflictsAsync(_repo.Path, ["a.txt"], ours: true));

        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("two");
        _repo.Run("ls-files", "-u").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Reset_soft_moves_the_branch_and_keeps_the_changes_staged()
    {
        Wait(_operations.ResetAsync(_repo.Path, "HEAD~1", ResetMode.Soft));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("first");
        _repo.Run("diff", "--cached", "--name-only").Trim().Should().Be("a.txt");
    }

    [AvaloniaTest]
    public void Reset_mixed_moves_the_branch_and_keeps_the_changes_unstaged()
    {
        Wait(_operations.ResetAsync(_repo.Path, "HEAD~1", ResetMode.Mixed));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("first");
        _repo.Run("diff", "--cached", "--name-only").Trim().Should().BeEmpty();
        _repo.Run("diff", "--name-only").Trim().Should().Be("a.txt");
    }

    [AvaloniaTest]
    public void Merge_of_a_branch_ahead_of_the_current_one_fast_forwards()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "feature");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "feature work");
        _repo.Run("checkout", "-q", baseBranch);

        Wait(_operations.MergeAsync(_repo.Path, "feature"));

        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("feature work");
    }

    [AvaloniaTest]
    public void Delete_remote_tag_removes_it_on_the_remote_and_keeps_the_local_tag()
    {
        string remote = CreateBareRemote();
        _repo.Run("remote", "add", "origin", remote);
        _repo.Run("tag", "v1.0");
        Wait(_operations.PushTagsAsync(_repo.Path, "origin"));

        Wait(_operations.DeleteRemoteTagAsync(_repo.Path, "origin", "v1.0"));

        GitProcess.Run(remote, "tag", "--list").Trim().Should().BeEmpty();
        _repo.Run("tag", "--list").Trim().Should().Be("v1.0");
    }

    [AvaloniaTest]
    public void Diff_tool_without_a_configured_tool_fails_with_a_hint()
    {
        // Empty local values override any tool in the developer's global config, so no real tool is started.
        _repo.Run("config", "diff.tool", "");
        _repo.Run("config", "diff.guitool", "");

        Action act = () => Wait(_operations.RunDiffToolAsync(_repo.Path, "a.txt", commit: null, staged: false));

        act.Should().Throw<GitOperationException>().WithMessage("*diff.tool*");
    }

    [AvaloniaTest]
    public void Merge_tool_without_a_configured_tool_fails_with_a_hint()
    {
        // Empty local values override any tool in the developer's global config, so no real tool is started.
        _repo.Run("config", "merge.tool", "");
        _repo.Run("config", "merge.guitool", "");

        Action act = () => Wait(_operations.RunMergeToolAsync(_repo.Path, "a.txt"));

        act.Should().Throw<GitOperationException>().WithMessage("*merge.tool*");
    }

    [AvaloniaTest]
    public void Conflicting_merge_fails_and_can_be_aborted()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "feature");
        _repo.Run("commit", "-q", "-am", "feature edit");
        _repo.Run("checkout", "-q", baseBranch);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "base");
        _repo.Run("commit", "-q", "-am", "base edit");

        Func<Task> merge = () => _operations.MergeAsync(_repo.Path, "feature");
        Action act = () => Wait(merge());
        act.Should().Throw<GitOperationException>();

        _repo.Run("status", "--porcelain").Should().Contain("UU a.txt");
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsMerging.Should().BeTrue();

        Wait(_operations.AbortMergeAsync(_repo.Path));

        _repo.Run("status", "--porcelain").Trim().Should().BeEmpty();
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("base");
    }

    [AvaloniaTest]
    public void Resolving_a_conflict_by_staging_and_committing_completes_the_merge()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "feature");
        _repo.Run("commit", "-q", "-am", "feature edit");
        _repo.Run("checkout", "-q", baseBranch);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "base");
        _repo.Run("commit", "-q", "-am", "base edit");
        Action merge = () => Wait(_operations.MergeAsync(_repo.Path, "feature"));
        merge.Should().Throw<GitOperationException>();

        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "resolved");
        Wait(_operations.StageAsync(_repo.Path, ["a.txt"]));
        Wait(_operations.CommitAsync(_repo.Path, "merge feature", amend: false, signOff: false, author: ""));

        _repo.Run("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Should().HaveCount(3);
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("merge feature");
        Wait(new GitRepositoryService().GetSnapshotAsync(_repo.Path)).IsMerging.Should().BeFalse();
    }

    private void StartConflictingMerge()
    {
        string baseBranch = CreateRebaseConflict();
        _repo.Run("checkout", "-q", baseBranch);
        Action merge = () => Wait(_operations.MergeAsync(_repo.Path, "feature"));
        merge.Should().Throw<GitOperationException>();
    }

    private string CreateRebaseConflict()
    {
        string baseBranch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "feature");
        _repo.Run("commit", "-q", "-am", "feature edit");
        return baseBranch;
    }

    // A stand-in for the app's editor: a shell script that git runs with the file to edit. Each script body writes
    // "$1.tmp" (or exits), which then replaces the file; sed -i is not used, because it differs between GNU and BSD.
    private string WriteEditorScript(string body)
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        _folders.Add(folder);
        string script = Path.Combine(folder, "editor.sh");
        File.WriteAllText(script, $"#!/bin/sh\n{body.ReplaceLineEndings("\n")}\nmv \"$1.tmp\" \"$1\"\n");
        return $"sh '{script.Replace('\\', '/')}'";
    }

    private void SaveStash(string content)
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), content);
        _repo.Run("stash", "push", "-q", "-m", "saved " + content);
    }

    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    private sealed class CollectingProgress(List<GitOutputLine> lines) : IProgress<GitOutputLine>
    {
        public void Report(GitOutputLine value)
        {
            lock (lines)
            {
                lines.Add(value);
            }
        }
    }

    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private string CreateBareRemote()
    {
        string remote = NewFolder();
        GitProcess.Run(remote, "init", "--bare", "-q");
        return remote;
    }

    private string NewFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "xplat-git-" + Guid.NewGuid().ToString("N"));
        _folders.Add(folder);
        Directory.CreateDirectory(folder);
        return folder;
    }
}
