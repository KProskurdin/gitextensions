using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class TextReplaceTests
{
    [Test]
    public void ReplaceAll_should_replace_each_occurrence_once_even_when_the_replacement_holds_it()
    {
        const string text = "red and RED";

        TextReplace.ReplaceAll(text, new TextSearch(text) { LookFor = "red" }, "very red")
            .Should().Be(("very red and very red", 2));
    }

    [Test]
    public void ReplaceAll_should_follow_the_search_options_and_its_region()
    {
        const string text = "red redder Red";

        TextReplace.ReplaceAll(text, new TextSearch(text) { LookFor = "red", MatchCase = true, MatchWholeWordOnly = true }, "x")
            .Should().Be(("x redder Red", 1));
        TextReplace.ReplaceAll(text, new TextSearch(text) { LookFor = "red", ScanRegion = (4, 6) }, "x")
            .Should().Be(("red xder Red", 1));
        TextReplace.ReplaceAll(text, new TextSearch(text) { LookFor = "blue" }, "x").Should().Be((text, 0));
    }

    [Test]
    public void MatchesSelection_should_ignore_the_case_as_upstream()
    {
        TextReplace.MatchesSelection("RED", "red").Should().BeTrue();
        TextReplace.MatchesSelection("red ", "red").Should().BeFalse();
    }

    [Test]
    public void ReadSelection_should_take_a_selection_a_region_or_the_word_at_the_caret()
    {
        const string text = "pick abc_1 first\npick def second\n";

        TextReplace.ReadSelection(text, 5, 8).Should().Be(("abc", null as (int, int)?));
        TextReplace.ReadSelection(text, 8, 5).Should().Be(("abc", null as (int, int)?), "a selection may run backward");
        TextReplace.ReadSelection(text, 5, 22).Should().Be(("", ((int, int)?)(5, 17)));
        TextReplace.ReadSelection(text, 7, 7).Should().Be(("abc_1", null as (int, int)?));
        TextReplace.ReadSelection(text, text.Length, text.Length).Should().Be(("", null as (int, int)?));
    }

    [Test]
    public void FindMatch_should_stop_once_at_each_group_of_matching_lines()
    {
        GrepLine[] lines =
        [
            new(1, GrepLineKind.Context, "a", []),
            new(2, GrepLineKind.Match, "b", []),
            new(3, GrepLineKind.Match, "c", []),
            new(null, GrepLineKind.Separator, "--", []),
            new(9, GrepLineKind.Match, "d", []),
        ];

        GitGrep.FindMatch(lines, -1, forward: true).Should().Be(1);
        GitGrep.FindMatch(lines, 1, forward: true).Should().Be(4, "the line after a match is part of its group");
        GitGrep.FindMatch(lines, 4, forward: true).Should().BeNull();
        GitGrep.FindMatch(lines, 4, forward: false).Should().Be(1);
        GitGrep.FindMatch(lines, 1, forward: false).Should().BeNull();
        GitGrep.FindMatch(lines, -1, forward: false).Should().Be(4);
    }

    [Test]
    public void Lines_should_be_counted_from_one_and_stop_at_the_end()
    {
        const string text = "a\nbb\nccc";

        TextReplace.LineOf(text, text.Length).Should().Be(3);
        TextReplace.OffsetOfLine(text, 1).Should().Be(0);
        TextReplace.OffsetOfLine(text, 3).Should().Be(5);
        TextReplace.OffsetOfLine(text, 9).Should().Be(text.Length);
    }
}
