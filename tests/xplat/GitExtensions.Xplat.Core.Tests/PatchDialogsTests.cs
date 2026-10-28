using System.Text;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PatchDialogsTests
{
    [Test]
    public void Ranges_should_follow_upstreams_three_cases()
    {
        FormatPatchDialog.Ranges([]).Should().BeEmpty();
        FormatPatchDialog.Ranges([("b", "a")]).Should().Equal(new FormatPatchRange("a", "b", null));
        FormatPatchDialog.Ranges([("b", "a"), ("d", "c")]).Should().Equal(new FormatPatchRange("a", "d", null));
        FormatPatchDialog.Ranges([("a", null), ("b", "a"), ("c", "b")]).Should().Equal(
            new FormatPatchRange("", "a", 1), new FormatPatchRange("a", "b", 2), new FormatPatchRange("b", "c", 3));
    }

    [Test]
    public void Read_should_split_the_file_into_its_file_patches_and_ignore_a_missing_file()
    {
        string path = Path.Combine(Path.GetTempPath(), "xplat-" + Guid.NewGuid().ToString("N") + ".patch");
        File.WriteAllText(path, string.Join("\n",
            "From 1234567890123456789012345678901234567890 Mon Sep 17 00:00:00 2001",
            "From: Test <test@example.com>",
            "Subject: [PATCH] change",
            "",
            "---",
            "diff --git a/a.txt b/a.txt",
            "index 1111111..2222222 100644",
            "--- a/a.txt",
            "+++ b/a.txt",
            "@@ -1 +1 @@",
            "-one",
            "+two",
            "diff --git a/new.txt b/new.txt",
            "new file mode 100644",
            "index 0000000..3333333",
            "--- /dev/null",
            "+++ b/new.txt",
            "@@ -0,0 +1 @@",
            "+new",
            ""));
        try
        {
            IReadOnlyList<Patch> patches = ViewPatchDialog.Read(path, () => Encoding.UTF8);

            patches.Select(patch => patch.FileNameB).Should().Equal("a.txt", "new.txt");
            patches[1].ChangeType.Should().Be(PatchChangeType.NewFile);
            patches[0].Text.Should().Contain("+two");
        }
        finally
        {
            File.Delete(path);
        }

        ViewPatchDialog.Read(path, () => Encoding.UTF8).Should().BeEmpty();
    }
}
