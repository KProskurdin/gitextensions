using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class MergeSubmoduleDialogTests
{
    private static readonly ObjectId _base = ObjectId.Parse("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
    private static readonly ObjectId _local = ObjectId.Parse("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
    private static readonly ObjectId _remote = ObjectId.Parse("cccccccccccccccccccccccccccccccccccccccc");

    [Test]
    public void The_texts_are_upstreams()
    {
        UpstreamSource("src/app/GitUI/CommandsDialogs/FormMergeSubmodule.cs").Should()
            .Contain($"\"{MergeSubmoduleDialog.StageFilename}\"").And.Contain($"\"{MergeSubmoduleDialog.Deleted}\"");
        string designer = UpstreamSource("src/app/GitUI/CommandsDialogs/FormMergeSubmodule.Designer.cs");
        foreach (string text in new[]
                 {
                     "Submodule conflict", "There is a conflict on the submodule:", "Base:", "Local:", "Remote:",
                     "Current:", "Stage Current", "Open submodule", "Checkout Branch",
                 })
        {
            designer.Should().Contain($"\"{text}\"");
        }
    }

    [Test]
    public void The_sides_show_their_commits_or_deleted()
    {
        ConflictData item = new(new ConflictedFileData(_base, "sub"), new ConflictedFileData(_local, "sub"),
            new ConflictedFileData(_remote, "sub"));
        ConflictData deleted = new(new ConflictedFileData(_base, "sub"), new ConflictedFileData(_local, "sub"),
            new ConflictedFileData(default(ObjectId), "sub"));

        MergeSubmoduleDialog.Commits(deleted).Should().Be((_base.ToString(), _local.ToString(), "deleted"));
        MergeSubmoduleDialog.Commits(deleted, text => text == "deleted" ? "gelöscht" : text).Remote.Should().Be("gelöscht");
        MergeSubmoduleDialog.CanCheckoutBranch(deleted).Should().BeFalse("the remote side has no commit");
        MergeSubmoduleDialog.CanCheckoutBranch(item).Should().BeTrue();
        MergeSubmoduleDialog.BranchCommits(deleted).Should().Equal(_local, default(ObjectId));
    }

    [Test]
    public void The_branches_to_check_out_contain_every_commit()
    {
        Dictionary<ObjectId, string[]> containing = new()
        {
            [_local] = ["main", "feature", "(HEAD detached at bbbbbbb)", "origin/HEAD"],
            [_remote] = ["feature", "other", "origin/HEAD"],
        };

        CheckoutBranchDialog.ContainingBranches([_local, _remote], id => containing[id]).Should().Equal("feature");
        CheckoutBranchDialog.ContainingBranches([_local], id => containing[id]).Should()
            .BeEquivalentTo(new[] { "main", "feature" });
        CheckoutBranchDialog.ContainingBranches([], id => containing[id]).Should().BeEmpty();
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
