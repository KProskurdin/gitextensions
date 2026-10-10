using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SyntaxHighlightingTests
{
    [Test]
    public void Every_upstream_definition_is_read()
    {
        SyntaxDefinitions.All.Count.Should().BeGreaterThan(80);
        SyntaxDefinitions.ForFile("src/Program.cs")!.Name.Should().Be("C#");
        SyntaxDefinitions.ForFile("run.BAT")!.Name.Should().Be("Batch");
        SyntaxDefinitions.ForFile("README").Should().BeNull();
    }

    [Test]
    public void A_csharp_line_has_upstreams_keyword_string_comment_and_number_colors()
    {
        SyntaxHighlighter highlighter = new(SyntaxDefinitions.ForFile("a.cs")!);
        const string line = "int x = 42; string s = \"hi\"; // note";

        IReadOnlyList<SyntaxRun> runs = highlighter.HighlightLine(line);

        StyleOf(line, runs, "int").Should().Be("#2d7c6d", "upstream colors type keywords apart");
        StyleOf(line, runs, "42").Should().Be("DarkBlue");
        StyleOf(line, runs, "\"hi\"").Should().Be("Maroon");
        StyleOf(line, runs, "// note").Should().Be("Green");
        StyleOf(line, runs, "x").Should().BeNull();
        runs.Sum(run => run.Length).Should().Be(line.Length);
    }

    [Test]
    public void A_block_comment_carries_on_to_the_next_line_until_it_ends()
    {
        SyntaxHighlighter highlighter = new(SyntaxDefinitions.ForFile("a.cs")!);

        highlighter.HighlightLine("int a; /* start");
        IReadOnlyList<SyntaxRun> second = highlighter.HighlightLine("still int */ int b;");

        StyleOf("still int */ int b;", second, "still").Should().Be("Green");
        StyleOf("still int */ int b;", second, " int b").Should().Be("#2d7c6d");

        highlighter.HighlightLine("int a; /* again");
        highlighter.Reset();
        StyleOf("int c;", highlighter.HighlightLine("int c;"), "int").Should().Be("#2d7c6d");
    }

    // The color of the run that covers the first character of the first occurrence of the text (after leading spaces).
    private static string? StyleOf(string line, IReadOnlyList<SyntaxRun> runs, string text)
    {
        int index = line.IndexOf(text, StringComparison.Ordinal) + (text.Length - text.TrimStart().Length);
        return runs.Single(run => run.Start <= index && index < run.Start + run.Length).Style?.Color;
    }
}
