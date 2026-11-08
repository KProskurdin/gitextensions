using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class NonPrintingCharactersTests
{
    [TestCase(LineEnding.LineFeed, false, @"\n")]
    [TestCase(LineEnding.CarriageReturnLineFeed, false, @"\r\n")]
    [TestCase(LineEnding.LineFeed, true, "¶")]
    [TestCase(LineEnding.CarriageReturnLineFeed, true, "¤")]
    [TestCase(LineEnding.None, false, "")]
    [TestCase(LineEnding.None, true, "")]
    public void EndOfLineMark_should_be_upstreams_text_or_glyph(LineEnding ending, bool asGlyph, string mark)
        => NonPrintingCharacters.EndOfLineMark(ending, asGlyph).Should().Be(mark);

    [Test]
    public void ParseText_should_keep_each_lines_ending_and_none_for_a_last_line_without_a_newline()
    {
        IReadOnlyList<DiffLine> lines = DiffParser.ParseText("unix\nwindows\r\nlast");

        lines.Select(line => line.Text).Should().Equal("unix", "windows", "last");
        lines.Select(line => line.Ending).Should()
            .Equal(LineEnding.LineFeed, LineEnding.CarriageReturnLineFeed, LineEnding.None);
    }

    [Test]
    public void ParseText_should_give_the_last_line_its_newline_when_the_text_ends_with_one()
        => DiffParser.ParseText("one\n").Single().Ending.Should().Be(LineEnding.LineFeed);

    [Test]
    public void Parse_should_keep_the_line_endings_of_a_hunks_lines()
    {
        const string diff = "diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -1,1 +1,1 @@\n-old\r\n+new\n";

        IReadOnlyList<DiffLine> lines = DiffParser.Parse(diff);

        lines.Single(line => line.Kind == DiffLineKind.Removed).Ending.Should().Be(LineEnding.CarriageReturnLineFeed);
        lines.Single(line => line.Kind == DiffLineKind.Added).Ending.Should().Be(LineEnding.LineFeed);
    }
}
