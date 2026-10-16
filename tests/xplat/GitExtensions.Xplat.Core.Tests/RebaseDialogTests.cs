using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RebaseDialogTests
{
    // Upstream's git command configuration for rebase (GitCommandConfiguration), which every rebase command starts with.
    private const string Rebase = "-c rebase.autosquash=false rebase ";

    [Test]
    public void The_texts_are_upstreams()
    {
        string source = UpstreamSource("src/app/GitUI/CommandsDialogs/FormRebase.cs");
        foreach (string text in new[]
                 {
                     RebaseDialog.ContinueRebaseText, RebaseDialog.SolveConflictsText,
                     RebaseDialog.SolveConflictsHighlightedText, RebaseDialog.ContinueRebaseHighlightedText,
                     RebaseDialog.NoBranchSelected, RebaseDialog.HoverShowImageText,
                 })
        {
            source.Should().Contain($"\"{text}\"");
        }

        string patchFile = UpstreamSource("src/app/GitUI/UserControls/PatchFile.cs");
        foreach (string status in new[] { RebasePatch.SkippedText, RebasePatch.AppliedText, RebasePatch.ApplyingText })
        {
            patchFile.Should().Contain($"\"{status}\"");
        }
    }

    [Test]
    public void A_date_option_rules_out_the_other_and_the_interactive_options()
    {
        RebaseDialog dialog = new();
        (dialog.CanInteractive, dialog.CanPreserveMerges, dialog.CanAutoSquash).Should().Be((true, true, false));

        dialog.Interactive = true;
        dialog.CanAutoSquash.Should().BeTrue();

        dialog.IgnoreDate = true;
        (dialog.CanCommitterDateIsAuthorDate, dialog.CanInteractive, dialog.CanPreserveMerges, dialog.CanAutoSquash)
            .Should().Be((false, false, false, false));
        dialog.CanIgnoreDate.Should().BeTrue();

        dialog.IgnoreDate = false;
        dialog.CommitterDateIsAuthorDate = true;
        (dialog.CanIgnoreDate, dialog.CanInteractive).Should().Be((false, false));
    }

    [Test]
    public void A_branch_to_rebase_on_is_needed()
    {
        new RebaseDialog().Validate().Should().Be(RebaseDialog.NoBranchSelected);
        new RebaseDialog { Onto = "main" }.Validate().Should().BeNull();
    }

    [Test]
    public void Arguments_should_be_upstreams()
    {
        RebaseDialog dialog = new() { Onto = "main" };
        dialog.Arguments(supportUpdateRefs: true, configuredUpdateRefs: false).ToString().Should().Be(Rebase + "\"main\"");

        dialog.Interactive = true;
        dialog.AutoSquash = true;
        dialog.PreserveMerges = true;
        dialog.AutoStash = true;
        dialog.UpdateRefs = true;
        dialog.Arguments(supportUpdateRefs: true, configuredUpdateRefs: false).ToString().Should()
            .Be(Rebase + "-i --autosquash --rebase-merges --update-refs --autostash \"main\"");
        dialog.Arguments(supportUpdateRefs: true, configuredUpdateRefs: true).ToString().Should()
            .Be(Rebase + "-i --autosquash --rebase-merges --autostash \"main\"", "the choice is git's setting");
        dialog.Arguments(supportUpdateRefs: false, configuredUpdateRefs: false).ToString().Should()
            .Be(Rebase + "-i --autosquash --rebase-merges --autostash \"main\"", "git does not support it");

        dialog = new RebaseDialog { Onto = "main", SpecificRange = true, From = "abc1234", To = " " };
        dialog.Arguments(false, false).ToString().Should().Be(Rebase + "\"main\"", "the range is incomplete");

        dialog.To = "topic";
        dialog.IgnoreDate = true;
        dialog.Interactive = true;
        dialog.Arguments(false, false).ToString().Should().Be(Rebase + "--ignore-date --onto main \"abc1234\" \"topic\"");
    }

    [Test]
    public void The_buttons_follow_the_rebase_state()
    {
        RebaseDialog.Buttons(inRebase: false, conflictedMerge: false, dirty: true).Should().Be(new RebaseButtons(
            CanChangeBranch: true, Rebase: true, AutoStash: true, Steps: false, Continue: false, SolveConflicts: false,
            UnresolvedConflicts: false, RebaseDefaultButton.Rebase));
        RebaseDialog.Buttons(inRebase: true, conflictedMerge: false, dirty: true).Should().Be(new RebaseButtons(
            CanChangeBranch: false, Rebase: false, AutoStash: false, Steps: true, Continue: true, SolveConflicts: false,
            UnresolvedConflicts: false, RebaseDefaultButton.Continue));
        RebaseDialog.Buttons(inRebase: true, conflictedMerge: true, dirty: true).Should().Be(new RebaseButtons(
            CanChangeBranch: false, Rebase: false, AutoStash: false, Steps: true, Continue: false, SolveConflicts: true,
            UnresolvedConflicts: true, RebaseDefaultButton.SolveConflicts));
    }

    [Test]
    public void The_status_is_upstreams()
    {
        new RebasePatch { IsApplied = true, IsNext = true }.Status.Should().Be(RebasePatch.AppliedText);
        new RebasePatch { IsNext = true }.Status.Should().Be(RebasePatch.ApplyingText);
        new RebasePatch { IsNext = true, IsSkipped = true }.Status.Should().Be(RebasePatch.SkippedText);
        new RebasePatch().Status.Should().BeEmpty();
        new RebasePatch { FullName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) }.Status.Should()
            .Be(RebasePatch.AppliedText, "a patch file that is gone was applied");
    }

    private static string UpstreamSource(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..", relativePath)));
}
