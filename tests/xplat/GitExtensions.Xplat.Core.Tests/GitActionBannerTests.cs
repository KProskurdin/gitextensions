using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitActionBannerTests
{
    private const string RepositoryPath = "/work/one";

    [Test]
    public void For_should_show_nothing_when_no_action_runs_and_nothing_conflicts()
    {
        GitActionBanner.For(GitAction.None, hasConflicts: false).Should().BeNull();
        GitActionBanner.ForRepository(isRebasing: false, isMerging: false, isApplyingPatch: false, hasConflicts: false)
            .Should().BeNull();
        GitActionBanner.ForBisect(isBisecting: false).Should().BeNull();
    }

    [TestCase(GitAction.Rebase, false, "Rebase is currently in progress.",
        new[] { GitActionButton.Continue, GitActionButton.Abort, GitActionButton.More })]
    [TestCase(GitAction.Rebase, true, "Rebase is currently in progress with merge conflicts.",
        new[] { GitActionButton.Resolve, GitActionButton.Abort, GitActionButton.More })]
    [TestCase(GitAction.Merge, false, "Merge is currently in progress.",
        new[] { GitActionButton.Continue, GitActionButton.Abort })]
    [TestCase(GitAction.Merge, true, "Merge is currently in progress with merge conflicts.",
        new[] { GitActionButton.Resolve, GitActionButton.Abort })]
    [TestCase(GitAction.Patch, false, "Patch is currently in progress.",
        new[] { GitActionButton.Continue, GitActionButton.Abort, GitActionButton.More })]
    [TestCase(GitAction.Bisect, false, "Bisect is currently in progress.", new[] { GitActionButton.More })]
    [TestCase(GitAction.None, true, "There are unresolved merge conflicts.", new[] { GitActionButton.Resolve })]
    public void For_should_give_upstreams_text_and_buttons_in_upstreams_order(GitAction action, bool conflicts,
        string text, GitActionButton[] buttons)
    {
        GitActionBanner banner = GitActionBanner.For(action, conflicts)!;

        banner.Text.Should().Be(text);
        banner.Buttons.Should().Equal(buttons);
        banner.HasConflicts.Should().Be(conflicts);
    }

    [Test]
    public void ForRepository_should_check_rebase_then_merge_then_patch_as_upstream()
    {
        GitActionBanner.ForRepository(isRebasing: true, isMerging: true, isApplyingPatch: false, hasConflicts: false)!
            .Action.Should().Be(GitAction.Rebase);
        GitActionBanner.ForRepository(isRebasing: false, isMerging: true, isApplyingPatch: true, hasConflicts: false)!
            .Action.Should().Be(GitAction.Merge);
        GitActionBanner.ForRepository(isRebasing: false, isMerging: false, isApplyingPatch: true, hasConflicts: true)!
            .Action.Should().Be(GitAction.Patch);
        GitActionBanner.ForBisect(isBisecting: true)!.Action.Should().Be(GitAction.Bisect);
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string control = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "UserControls",
            "InteractiveGitActionControl.cs"));

        control.Should().Contain("new(\"{0} is currently in progress.\")")
            .And.Contain("new(\"There are unresolved merge conflicts.\")")
            .And.Contain("new(\"{0} is currently in progress with merge conflicts.\")");
        foreach (GitAction action in new[] { GitAction.Bisect, GitAction.Rebase, GitAction.Merge, GitAction.Patch })
        {
            control.Should().Contain($"new(\"{action}\")");
        }
    }

    [Test]
    public void FromRefName_should_label_bisect_marks_and_leave_other_bisect_refs_out()
    {
        RefLabel.FromRefName("refs/bisect/bad").Should().Be(new RefLabel("bad", RefKind.BisectBad));
        RefLabel.FromRefName("refs/bisect/good-0123abcd").Should().Be(new RefLabel("good", RefKind.BisectGood));
        RefLabel.FromRefName("refs/bisect/skip-0123abcd").Should().BeNull();
        new RefLabel("good", RefKind.BisectGood).IsBisect.Should().BeTrue();
        new RefLabel("main", RefKind.Branch).IsBisect.Should().BeFalse();
    }

    [Test]
    public void Message_tooltip_should_list_bisect_marks_first_with_upstreams_text()
    {
        GitRevision revision = new(ObjectId.Random()) { Subject = "Subject" };

        string? message = RevisionTooltips.For(revision,
        [
            new RefLabel("main", RefKind.Branch), new RefLabel("bad", RefKind.BisectBad),
        ]).Message;

        message.Should().Be($"Subject{Environment.NewLine}{Environment.NewLine}{RevisionTooltips.MarkedBad}" +
                            $"{Environment.NewLine}[main]{Environment.NewLine}");
        File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "TranslatedStrings.cs")).Should()
            .Contain($"new(\"{RevisionTooltips.MarkedGood}\")").And.Contain($"new(\"{RevisionTooltips.MarkedBad}\")");
    }

    [Test]
    public async Task MarkBisectAsync_should_keep_the_output_open_and_StartBisectAsync_should_not()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git);

        Task<bool> start = viewModel.StartBisectAsync(RepositoryPath);
        viewModel.KeepOutputOpen.Should().BeFalse();
        git.Complete(0);
        (await start).Should().BeTrue();

        Task<bool> mark = viewModel.MarkBisectAsync(RepositoryPath, GitBisectOption.Bad, "0123456789abcdef");
        viewModel.KeepOutputOpen.Should().BeTrue();
        viewModel.OutputTitle.Should().Be("Bisect: mark 01234567 bad");
        git.ArgumentsAt(1).Should().Be($"{RepositoryPath} Bad 0123456789abcdef");
        git.Complete(1);
        (await mark).Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Marked 01234567 bad");
    }

    [Test]
    public async Task ContinueMergeAsync_should_pass_the_editor()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel viewModel = new(git) { EditorCommand = "'app' fileeditor" };

        Task<bool> continued = viewModel.ContinueMergeAsync(RepositoryPath);
        git.NameAt(0).Should().Be("ContinueMerge");
        git.ArgumentsAt(0).Should().Be($"{RepositoryPath} editor='app' fileeditor");
        git.Complete(0);

        (await continued).Should().BeTrue();
        viewModel.StatusMessage.Should().Be("Merge continued");
    }

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
