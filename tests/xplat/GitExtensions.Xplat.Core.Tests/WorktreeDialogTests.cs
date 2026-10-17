using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class WorktreeDialogTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private string _root = "";

    [SetUp]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "xplat-worktree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void The_texts_are_upstreams()
    {
        string strings = UpstreamSource("src/app/GitUI/TranslatedStrings.cs");
        foreach (string text in new[]
                 {
                     ManageWorktreeDialog.SwitchCaption, ManageWorktreeDialog.DeleteQuestion,
                     ManageWorktreeDialog.DeleteCaption, ManageWorktreeDialog.CannotBeUndone,
                     ManageWorktreeDialog.DeleteFailed, ManageWorktreeDialog.DirectoryNotFound,
                     ManageWorktreeDialog.CreateWorktree, ManageWorktreeDialog.PruneWorktrees,
                     ManageWorktreeDialog.ManageWorktrees, ManageWorktreeDialog.Error,
                     CreateWorktreeDialog.LoadingData,
                 })
        {
            strings.Should().Contain($"\"{text}\"");
        }

        string manage = UpstreamSource("src/app/GitUI/CommandsDialogs/WorktreeDialog/FormManageWorktree.Designer.cs");
        foreach (string text in new[]
                 {
                     "Existing worktrees", "&Open selected", "&Delete selected", "&Prune deleted worktrees",
                     "&Create...", "Path", "Type", "Branch", "SHA-1",
                 })
        {
            manage.Should().Contain($"\"{text}\"");
        }

        string create = UpstreamSource("src/app/GitUI/CommandsDialogs/WorktreeDialog/FormCreateWorktree.Designer.cs");
        foreach (string text in new[]
                 {
                     "Create a new worktree", "What to checkout:", "Checkout an &existing branch:",
                     "Create a &new branch:\\r\\n(from current commit)", "New worktree &directory:",
                     "&Create the new worktree",
                 })
        {
            create.Should().Contain($"\"{text}\"");
        }

        UpstreamSource("src/app/GitUI/UserControls/FolderBrowserButton.Designer.cs").Should().Contain("\"&Browse...\"");
    }

    [Test]
    public void Only_another_existing_worktree_can_be_opened_and_never_the_main_one_deleted()
    {
        string main = Path.Combine(_root, "repo");
        string other = Path.Combine(_root, "repo_feature");
        IReadOnlyList<WorktreeInfo> worktrees =
        [
            new(main, "repo", IsMain: true, IsDeleted: false),
            new(other, "other", IsMain: false, IsDeleted: false),
            new(Path.Combine(_root, "gone"), "gone", IsMain: false, IsDeleted: true),
        ];

        ManageWorktreeDialog.CanOpen(worktrees, 0, main + Path.DirectorySeparatorChar).Should().BeFalse("it is open");
        ManageWorktreeDialog.CanOpen(worktrees, 1, main).Should().BeTrue();
        ManageWorktreeDialog.CanOpen(worktrees, 2, main).Should().BeFalse("its folder is gone");
        ManageWorktreeDialog.CanOpen(worktrees, -1, main).Should().BeFalse();
        ManageWorktreeDialog.CanDelete(worktrees, 1, main).Should().BeTrue();
        ManageWorktreeDialog.CanOpen(worktrees, 0, other).Should().BeTrue();
        ManageWorktreeDialog.CanDelete(worktrees, 0, other).Should().BeFalse("the main worktree holds the repository");
        ManageWorktreeDialog.CanOpen([worktrees[0]], 0, other).Should().BeFalse("upstream needs more than one");
    }

    [Test]
    public void Prune_is_offered_when_a_worktree_other_than_the_main_one_lost_its_folder()
    {
        WorktreeInfo main = new("/work/repo", "repo", IsMain: true, IsDeleted: false);
        WorktreeInfo gone = new("/work/gone", "gone", IsMain: false, IsDeleted: true);

        ManageWorktreeDialog.CanPrune([main]).Should().BeFalse();
        ManageWorktreeDialog.CanPrune([main with { IsDeleted = true }]).Should().BeFalse();
        ManageWorktreeDialog.CanPrune([main, gone]).Should().BeTrue();
        ManageWorktreeDialog.MainWorktreePath([main, gone], "/elsewhere").Should().Be("/work/repo");
        ManageWorktreeDialog.MainWorktreePath([], "/elsewhere").Should().Be("/elsewhere");
    }

    [Test]
    public void The_drop_down_shows_the_folder_name_and_the_head_as_upstream()
    {
        WorktreeInfo branch = new(Path.Combine(_root, "repo_feature"), "", IsMain: false, IsDeleted: false,
            GitWorktreeHeadType.Branch, "feature", Sha);
        WorktreeInfo detached = branch with { HeadType = GitWorktreeHeadType.Detached, Branch = null };

        ManageWorktreeDialog.MenuText(branch).Should().Be("repo_feature (feature)");
        ManageWorktreeDialog.MenuText(detached).Should().Be("repo_feature (detached at 0123456)");
    }

    [Test]
    public void Create_proposes_a_folder_named_after_the_main_worktree_and_the_branch()
    {
        string main = Path.Combine(_root, "repo");
        CreateWorktreeDialog dialog = new(main, ["master", "feature/x", "fix"], currentBranch: "master");

        dialog.Branches.Should().Equal("feature/x", "fix");
        dialog.CreateNewBranch.Should().BeFalse();
        dialog.SelectedBranch.Should().Be("feature/x");
        dialog.Directory.Should().Be(main + "_feature_x");

        dialog.CreateNewBranch = true;
        dialog.NewBranchName = "topic.";

        dialog.ProposedDirectory().Should().Be(main + "_topic");
    }

    [Test]
    public void Create_starts_with_a_new_branch_when_no_other_branch_exists()
    {
        CreateWorktreeDialog dialog = new(Path.Combine(_root, "repo"), ["master"], currentBranch: "master");

        dialog.CanCheckoutExistingBranch.Should().BeFalse();
        dialog.CreateNewBranch.Should().BeTrue();
        dialog.SelectedBranch.Should().BeNull();
    }

    [Test]
    public void Create_needs_a_new_branch_name_no_branch_has_and_an_empty_or_new_folder()
    {
        CreateWorktreeDialog dialog = new(Path.Combine(_root, "repo"), ["master", "fix"], currentBranch: "master");
        dialog.CanCreate().Should().BeTrue();

        dialog.CreateNewBranch = true;
        dialog.CanCreate().Should().BeFalse("no name is given");
        dialog.NewBranchName = "master";
        dialog.CanCreate().Should().BeFalse("upstream checks the current branch too");
        dialog.NewBranchName = "topic";
        dialog.CanCreate().Should().BeTrue();

        Directory.CreateDirectory(dialog.Directory);
        dialog.CanCreate().Should().BeTrue("an empty folder can take the worktree");
        File.WriteAllText(Path.Combine(dialog.Directory, "f.txt"), "");
        dialog.CanCreate().Should().BeFalse();
        dialog.Directory = " ";
        dialog.CanCreate().Should().BeFalse();
    }

    [Test]
    public void Create_runs_worktree_add_with_the_relative_folder_as_upstream()
    {
        string main = Path.Combine(_root, "repo");
        CreateWorktreeDialog dialog = new(main, ["master", "fix"], currentBranch: "master");

        dialog.Arguments(main, useRelativePathsSet: false).ToString().Should()
            .Be("-c worktree.useRelativePaths=true worktree add \"../repo_fix\" fix");

        dialog.CreateNewBranch = true;
        dialog.NewBranchName = "topic";
        dialog.Directory = dialog.ProposedDirectory();

        dialog.Arguments(main, useRelativePathsSet: true).ToString().Should()
            .Be("worktree add \"../repo_topic\" -b topic");
    }

    private static string UpstreamSource(string relativePath,
        [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
