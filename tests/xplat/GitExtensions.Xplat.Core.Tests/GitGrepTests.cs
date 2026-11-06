using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitGrepTests
{
    private const string Match = "\u001b[1;31m";
    private const string Reset = "\u001b[m";

    [Test]
    public void SearchArgument_should_make_a_text_one_pattern_as_upstream()
    {
        GitGrep.SearchArgument("needle").Should().Be("-e \"needle\"");
        GitGrep.SearchArgument(@"a\\b").Should().Be(@"-e ""a\\\\b""");
        GitGrep.SearchArgument("-e one -e two").Should()
            .Be("-e one -e two", "a text with -e is git-grep's own arguments");
        GitGrep.SearchArgument(" ").Should().BeEmpty();
        GitGrep.SearchArgument(null).Should().BeEmpty();
    }

    [Test]
    public void Parse_should_read_the_numbers_kinds_and_marked_matches()
    {
        IReadOnlyList<GrepLine> lines = GitGrep.Parse($"3:a {Match}needle{Reset} b\n4-after\n");

        lines.Should().HaveCount(2);
        lines[0].Should().BeEquivalentTo(new GrepLine(3, GrepLineKind.Match, "a needle b", [(2, 6)]));
        lines[1].Should().BeEquivalentTo(new GrepLine(4, GrepLineKind.Context, "after", []));
    }

    [Test]
    public void Parse_should_keep_a_separator_only_between_parts_and_never_before_a_function_line()
    {
        string output =
            $"--\n2:{Match}x{Reset}\n--\n9=\u001b[2;7mvoid f()\u001b[m\n10:{Match}x{Reset}\n--\n20:{Match}x{Reset}\n";

        IReadOnlyList<GrepLine> lines = GitGrep.Parse(output);

        lines.Select(line => (line.Number, line.Kind)).Should().Equal(
            (2, GrepLineKind.Match),
            (9, GrepLineKind.Function),
            (10, GrepLineKind.Match),
            (null, GrepLineKind.Separator),
            (20, GrepLineKind.Match));
        lines[1].Text.Should().Be("void f()");
        lines[1].Matches.Should().BeEmpty("upstream colors the whole function line, not a match");
    }

    [Test]
    public void Parse_should_mark_several_matches_on_a_line()
    {
        GrepLine line = GitGrep.Parse($"1:{Match}ab{Reset}-{Match}ab{Reset}\n").Single();

        line.Text.Should().Be("ab-ab");
        line.Matches.Should().Equal((0, 2), (3, 2));
    }
}
