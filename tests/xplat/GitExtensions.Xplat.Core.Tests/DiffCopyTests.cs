using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffCopyTests
{
    private static readonly DiffLine[] _lines =
    [
        new("@@ -1,3 +1,3 @@", DiffLineKind.Hunk),
        new(" same", DiffLineKind.Context, 1, 1),
        new("-old", DiffLineKind.Removed, 2),
        new("+new", DiffLineKind.Added, NewNumber: 2),
    ];

    [Test]
    public void Text_should_drop_the_prefixes_of_a_diffs_content_lines()
    {
        DiffCopy.Text(_lines, isDiff: true).Should().Be("@@ -1,3 +1,3 @@\nsame\nold\nnew");
        DiffCopy.Text([new DiffLine("+not a prefix", DiffLineKind.Context)], isDiff: false).Should().Be("+not a prefix");
    }

    [Test]
    public void Patch_should_keep_the_lines_as_they_are()
    {
        DiffCopy.Patch(_lines).Should().Be("@@ -1,3 +1,3 @@\n same\n-old\n+new");
    }

    [Test]
    public void Version_should_leave_out_the_other_sides_lines()
    {
        DiffCopy.Version(_lines, isDiff: true, newVersion: true).Should().Be("@@ -1,3 +1,3 @@\nsame\nnew");
        DiffCopy.Version(_lines, isDiff: true, newVersion: false).Should().Be("@@ -1,3 +1,3 @@\nsame\nold");
    }
}
