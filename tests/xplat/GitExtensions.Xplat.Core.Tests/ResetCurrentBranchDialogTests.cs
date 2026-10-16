using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ResetCurrentBranchDialogTests
{
    [Test]
    public void The_texts_are_upstreams()
    {
        string source = UpstreamSource("src/app/GitUI/HelperDialogs/FormResetCurrentBranch.cs");
        foreach (string text in new[]
                 {
                     ResetCurrentBranchDialog.BranchInfo, ResetCurrentBranchDialog.ResetHardWarning,
                     ResetCurrentBranchDialog.ResetCaption,
                 })
        {
            source.Should().Contain($"\"{text}\"");
        }

        string reflog = UpstreamSource("src/app/GitUI/CommandsDialogs/FormReflog.cs");
        reflog.Should().Contain("\"You have changes in your working directory that could be lost.\\n\\nDo you want to continue?\"")
            .And.Contain("\"Changes not committed...\"").And.Contain("\"current branch (\"");
    }

    [Test]
    public void Only_a_hard_reset_asks_first()
    {
        ResetCurrentBranchDialog dialog = new();
        dialog.Mode.Should().Be(ResetMode.Soft);
        dialog.AsksFirst.Should().BeFalse();

        dialog.Mode = ResetMode.Hard;

        dialog.AsksFirst.Should().BeTrue();
        ResetCurrentBranchDialog.Modes.Should()
            .Equal(ResetMode.Soft, ResetMode.Mixed, ResetMode.Keep, ResetMode.Merge, ResetMode.Hard);
    }

    [Test]
    public void Submodules_are_updated_after_a_hard_reset_or_as_the_setting_says()
    {
        ResetCurrentBranchDialog soft = new();
        ResetCurrentBranchDialog hard = new(ResetMode.Hard);

        hard.UpdatesSubmodules(movesHead: true, updateSubmodulesOnCheckout: false, hasSubmodules: false).Should().BeTrue();
        hard.UpdatesSubmodules(movesHead: false, updateSubmodulesOnCheckout: true, hasSubmodules: true).Should().BeFalse();
        soft.UpdatesSubmodules(movesHead: true, updateSubmodulesOnCheckout: null, hasSubmodules: true).Should().BeFalse();
        soft.UpdatesSubmodules(movesHead: true, updateSubmodulesOnCheckout: true, hasSubmodules: true).Should().BeTrue();
        soft.UpdatesSubmodules(movesHead: true, updateSubmodulesOnCheckout: true, hasSubmodules: false).Should().BeFalse();
    }

    [Test]
    public void The_help_is_gits_documentation_of_the_reset_type()
    {
        new ResetCurrentBranchDialog(ResetMode.Keep).HelpUrl().Should()
            .Be("https://git-scm.com/docs/git-reset#Documentation/git-reset.txt---keep");
    }

    [Test]
    public void The_reflog_lines_are_read_as_upstream_reads_them()
    {
        const string sha = "0123456789abcdef0123456789abcdef01234567";
        IReadOnlyList<ReflogEntry> entries = Reflog.Parse(
            $"{sha} HEAD@{{0}}: reset: moving to HEAD~1\n\n{sha} HEAD@{{1}}: commit: a: b\nnot a reflog line\n");

        entries.Should().Equal(
            new ReflogEntry(ObjectId.Parse(sha), "HEAD@{0}", "reset: moving to HEAD~1"),
            new ReflogEntry(ObjectId.Parse(sha), "HEAD@{1}", "commit: a: b"));
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
