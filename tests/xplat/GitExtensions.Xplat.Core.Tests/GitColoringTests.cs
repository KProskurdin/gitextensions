using System.Drawing;
using AwesomeAssertions;
using GitCommands;
using GitExtensions.Xplat.Core.Diff;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

[NonParallelizable]
internal sealed class GitColoringTests
{
    private const string Reset = "\u001b[m";
    private const string ReverseRed = "\u001b[7;31m";
    private const string ReverseGreen = "\u001b[7;32m";
    private const string Cyan = "\u001b[36m";
    private const string Bold = "\u001b[1m";

    private Func<AppColor, Color> _themeColors = null!;
    private bool _reverse;

    [SetUp]
    public void SetUp()
    {
        _themeColors = AppColorExtension.ThemeColors;
        AppColorExtension.ThemeColors = ThemeColor;
        _reverse = AppSettings.ReverseGitColoring.Value;
        AppSettings.ReverseGitColoring.Value = true;
    }

    [TearDown]
    public void TearDown()
    {
        AppColorExtension.ThemeColors = _themeColors;
        AppSettings.ReverseGitColoring.Value = _reverse;
    }

    [Test]
    public void ParseDiff_should_remove_the_codes_and_give_each_line_its_kind_numbers_and_colors()
    {
        string colored =
            $"{Bold}diff --git a/a b/a{Reset}\n{Bold}--- a/a{Reset}\n{Bold}+++ b/a{Reset}\n" +
            $"{Cyan}@@ -1,2 +1,2 @@{Reset}\n ctx\n{ReverseRed}-old{Reset}\n{ReverseGreen}+new{Reset}\n";

        IReadOnlyList<DiffLine> lines = GitColoring.ParseDiff(colored);

        lines.Select(line => line.Text).Should().Equal(
            "diff --git a/a b/a", "--- a/a", "+++ b/a", "@@ -1,2 +1,2 @@", " ctx", "-old", "+new");
        lines.Select(line => line.Kind).Should().Equal(DiffLineKind.Header, DiffLineKind.Header, DiffLineKind.Header,
            DiffLineKind.Hunk, DiffLineKind.Context, DiffLineKind.Removed, DiffLineKind.Added);
        (lines[4].OldNumber, lines[4].NewNumber).Should().Be((1, 1));
        (lines[5].OldNumber, lines[5].NewNumber).Should().Be((2, (int?)null));
        (lines[6].OldNumber, lines[6].NewNumber).Should().Be(((int?)null, 2));

        lines[4].ColorRuns.Should().BeEmpty("git colors nothing on a context line");
        lines[5].ColorRuns.Should().ContainSingle().Which.Background.Should().Be(ThemeColor(AppColor.AnsiTerminalRedBackNormal));
        DiffColorRun added = lines[6].ColorRuns!.Should().ContainSingle().Subject;
        (added.Start, added.Length).Should().Be((0, 4));
        added.Background.Should().Be(ThemeColor(AppColor.AnsiTerminalGreenBackNormal));
    }

    [Test]
    public void ParseDiff_should_keep_a_lines_ending_out_of_its_text_and_colors()
    {
        string colored = $"{Cyan}@@ -1 +1 @@{Reset}\n{ReverseGreen}+new\r{Reset}\n";

        DiffLine line = GitColoring.ParseDiff(colored).Last();

        line.Text.Should().Be("+new");
        line.Ending.Should().Be(LineEnding.CarriageReturnLineFeed);
        line.ColorRuns.Should().ContainSingle().Which.Length.Should().Be(4);
    }

    [Test]
    public void ParseDiff_of_a_word_diff_should_guess_a_lines_kind_from_its_colors_as_upstream()
    {
        string colored =
            $"{Cyan}@@ -5,3 +5,3 @@{Reset}\nsame\nsame {ReverseRed}old{Reset}{ReverseGreen}new{Reset} end\n" +
            $"{ReverseGreen}all new{Reset}\n{ReverseRed}all old{Reset}\n";

        IReadOnlyList<DiffLine> lines = GitColoring.ParseDiff(colored, isGitWordDiff: true);

        lines.Select(line => line.Text).Should().Equal("@@ -5,3 +5,3 @@", "same", "same oldnew end", "all new", "all old");
        lines.Select(line => line.Kind).Should().Equal(DiffLineKind.Hunk, DiffLineKind.Context, DiffLineKind.Context,
            DiffLineKind.Added, DiffLineKind.Removed);
        (lines[1].OldNumber, lines[1].NewNumber).Should().Be((5, 5));
        (lines[2].OldNumber, lines[2].NewNumber).Should().Be((6, 6));
        (lines[3].OldNumber, lines[3].NewNumber).Should().Be(((int?)null, 7));
        (lines[4].OldNumber, lines[4].NewNumber).Should().Be((7, (int?)null));
        lines[2].ColorRuns!.Select(run => (run.Start, run.Length)).Should().Equal((5, 3), (8, 3));
    }

    [Test]
    public void ParseRangeDiff_should_number_the_lines_and_make_a_commits_line_a_header()
    {
        string colored = $"{Bold}1:  abc = 1:  def subject{Reset}\n    context\n-:  abc < -:  --- other\n";

        IReadOnlyList<DiffLine> lines = GitColoring.ParseRangeDiff(colored);

        lines.Select(line => line.Kind).Should().Equal(DiffLineKind.Header, DiffLineKind.Context, DiffLineKind.Header);
        lines.Select(line => line.NewNumber).Should().Equal(1, 2, 3);
        lines[0].Text.Should().Be("1:  abc = 1:  def subject");
    }

    [Test]
    public void The_options_add_the_word_diff_argument_and_take_git_colors_for_it()
    {
        DiffOptions patch = new();
        patch.UsesGitColors.Should().BeFalse();
        patch.Arguments.Should().NotContain("--word-diff=color");
        (patch with { UseGitColoring = true }).UsesGitColors.Should().BeTrue();

        DiffOptions word = patch with { Appearance = GitCommands.Settings.DiffDisplayAppearance.GitWordDiff };

        word.UsesGitColors.Should().BeTrue();
        word.Arguments.Should().Contain("--word-diff=color");
    }

    [Test]
    public void Difftastic_Parse_should_take_out_the_line_numbers_and_read_changes_from_their_colors()
    {
        const int width = 40;
        string red = "\u001b[48;2;255;0;0m";
        string green = "\u001b[48;2;0;255;0m";
        string gray = "\u001b[48;2;9;9;9m";
        string colored =
            "a.txt --- Text\n" +
            $"{gray}1 {Reset}same".PadRight(gray.Length + Reset.Length + 20) + $"{gray}1 {Reset}same\n" +
            $"{red}2 {Reset}old\n" +
            $"{green}2 {Reset}new\n" +
            "\n" +
            "b.txt --- Text\n";

        IReadOnlyList<DiffLine> lines = Difftastic.Parse(colored, width, reverseGitColoring: true);

        lines.Select(line => line.Kind).Should().Equal(DiffLineKind.Header, DiffLineKind.Context, DiffLineKind.Removed,
            DiffLineKind.Added, DiffLineKind.Header);
        lines[0].Text.Should().Be("a.txt --- Text");
        lines[4].Text.Should().Be("b.txt --- Text", "an empty line comes before a header");
        (lines[1].OldNumber, lines[1].NewNumber).Should().Be((1, 1));
        lines[1].Text.Should().Be("same" + new string(' ', 14) + "same");
        (lines[2].OldNumber, lines[2].NewNumber, lines[2].Text).Should().Be((2, (int?)null, "old"));
        (lines[3].OldNumber, lines[3].NewNumber, lines[3].Text).Should().Be(((int?)null, 2, "new"));
    }

    [TestCase(0, 88)]
    [TestCase(1000, 142)]
    [TestCase(1001, 142)]
    [TestCase(5000, 200)]
    public void Difftastic_WidthFor_should_be_even_and_between_upstreams_limits(double pixels, int width)
        => Difftastic.WidthFor(pixels).Should().Be(width);

    // A color of its own for each of the theme's.
    private static Color ThemeColor(AppColor name) => Color.FromArgb(255, 100, (int)name % 256, 50);
}
