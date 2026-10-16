using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class MergeBranchDialogTests
{
    private static readonly MergeRepositorySettings _defaults = new(NoFastForward: false, AddLogMessages: false, LogMessagesCount: 20);

    [Test]
    public void The_text_is_upstreams()
        => UpstreamSource("src/app/GitUI/CommandsDialogs/FormMergeBranch.cs").Should()
            .Contain($"\"{MergeBranchDialog.HoverShowImageText}\"");

    [Test]
    public void A_merge_commit_cannot_be_squashed()
    {
        MergeBranchDialog dialog = new(_defaults, noCommit: false, advanced: true) { Squash = true };
        dialog.CanSquash.Should().BeTrue();

        dialog.NoFastForward = true;

        dialog.Squash.Should().BeFalse();
        dialog.CanSquash.Should().BeFalse();
    }

    [Test]
    public void Hiding_the_advanced_options_clears_them()
    {
        MergeBranchDialog dialog = new(_defaults, noCommit: false, advanced: true)
        {
            UseStrategy = true,
            Strategy = "ours",
            Squash = true,
            AllowUnrelatedHistories = true,
            AddMergeMessage = true,
        };

        dialog.Advanced = false;

        (dialog.UseStrategy, dialog.Strategy, dialog.Squash, dialog.AllowUnrelatedHistories, dialog.AddMergeMessage).Should()
            .Be((false, "", false, false, false));
    }

    [Test]
    public void The_repository_settings_start_the_dialog_and_are_kept_from_it()
    {
        MergeBranchDialog dialog = new(new MergeRepositorySettings(true, true, 7), noCommit: true, advanced: false);

        (dialog.NoFastForward, dialog.AddLogMessages, dialog.LogMessagesCount, dialog.NoCommit).Should().Be((true, true, 7, true));

        dialog.NoFastForward = false;
        dialog.LogMessagesCount = 9;
        dialog.RepositorySettings.Should().Be(new MergeRepositorySettings(false, true, 9));
    }

    [Test]
    public void Arguments_should_be_upstreams()
    {
        MergeBranchDialog dialog = new(_defaults, noCommit: false, advanced: true) { Branch = " feature " };
        dialog.Arguments(null, path => path).ToString().Should().Be("merge --ff --no-edit feature");

        dialog.NoFastForward = true;
        dialog.NoCommit = true;
        dialog.UseStrategy = true;
        dialog.Strategy = "ours";
        dialog.AllowUnrelatedHistories = true;
        dialog.AddLogMessages = true;
        dialog.LogMessagesCount = 5;
        dialog.AddMergeMessage = true;
        dialog.Arguments("/repo/.git/MERGE_MSG", path => path).ToString().Should()
            .Be("merge --no-ff --strategy=ours --no-commit --allow-unrelated-histories -F \"/repo/.git/MERGE_MSG\" --log=5 --no-edit feature");

        dialog.UseStrategy = false;
        dialog.AddMergeMessage = false;
        dialog.Arguments("/repo/.git/MERGE_MSG", path => path).ToString().Should()
            .Be("merge --no-ff --no-commit --allow-unrelated-histories --log=5 --no-edit feature");
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
