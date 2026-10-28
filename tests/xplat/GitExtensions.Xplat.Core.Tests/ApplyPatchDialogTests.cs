using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ApplyPatchDialogTests
{
    [Test]
    public void Buttons_should_follow_upstreams_three_states()
    {
        ApplyPatchButtons idle = ApplyPatchDialog.Buttons(inPatch: false, conflicted: false, fileMode: false);
        idle.Apply.Should().BeTrue();
        idle.DirectoryBox.Should().BeTrue();
        idle.FileBox.Should().BeFalse();
        idle.Skip.Should().BeFalse();
        idle.Default.Should().Be(ApplyPatchDefault.None);

        ApplyPatchButtons stopped = ApplyPatchDialog.Buttons(inPatch: true, conflicted: false, fileMode: true);
        stopped.Apply.Should().BeFalse();
        stopped.FileBox.Should().BeFalse();
        stopped.Resolved.Should().BeTrue();
        stopped.Mergetool.Should().BeFalse();
        stopped.Default.Should().Be(ApplyPatchDefault.Resolved);

        ApplyPatchButtons conflicted = ApplyPatchDialog.Buttons(inPatch: true, conflicted: true, fileMode: true);
        conflicted.Resolved.Should().BeFalse();
        conflicted.Mergetool.Should().BeTrue();
        conflicted.UnresolvedConflicts.Should().BeTrue();
        conflicted.Default.Should().Be(ApplyPatchDefault.Mergetool);
    }

    [Test]
    public void IsDiffFile_should_tell_a_raw_diff_from_a_mail()
    {
        string path = Path.Combine(Path.GetTempPath(), "xplat-" + Guid.NewGuid().ToString("N") + ".patch");
        try
        {
            File.WriteAllText(path, "diff --git a/a b/a\n");
            ApplyPatchDialog.IsDiffFile(path).Should().BeTrue();
            File.WriteAllText(path, "From 123 Mon Sep 17 00:00:00 2001\n");
            ApplyPatchDialog.IsDiffFile(path).Should().BeFalse();
        }
        finally
        {
            File.Delete(path);
        }

        ApplyPatchDialog.IsDiffFile(path).Should().BeFalse("a missing file is no diff");
        ApplyPatchDialog.AddFilesArguments(".", force: true, dryRun: true).Should().Be("add --dry-run -f .");
        ApplyPatchDialog.Highlighted("Conflicts resolved").Should().Be(">Conflicts resolved<");
    }
}
