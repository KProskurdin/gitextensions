using System.Drawing;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

[NonParallelizable]
internal sealed class InlineDifferencesTests
{
    private Func<AppColor, Color> _themeColors = null!;

    [SetUp]
    public void SetUp()
    {
        _themeColors = AppColorExtension.ThemeColors;
        AppColorExtension.ThemeColors = name => name switch
        {
            AppColor.AnsiTerminalRedBackNormal => Color.Red,
            AppColor.AnsiTerminalGreenBackNormal => Color.Lime,
            _ => Color.FromArgb(255, 200, (int)name % 256, 90)
        };
    }

    [TearDown]
    public void TearDown() => AppColorExtension.ThemeColors = _themeColors;

    [Test]
    public void A_replaced_line_has_what_it_shares_dimmed()
    {
        IReadOnlyList<DiffLine> lines = InlineDifferences.Add(
            [new DiffLine("-int value = 1;", DiffLineKind.Removed), new DiffLine("+int value = 2;", DiffLineKind.Added)],
            dimBackground: true);

        Dimmed(lines[0]).Should().Equal("int value = ", ";");
        Dimmed(lines[1]).Should().Equal("int value = ", ";");
    }

    [Test]
    public void A_whole_line_of_new_text_has_nothing_dimmed()
    {
        IReadOnlyList<DiffLine> lines = InlineDifferences.Add(
            [new DiffLine(" context", DiffLineKind.Context), new DiffLine("+only added", DiffLineKind.Added)], dimBackground: true);

        lines[0].DimRuns.Should().BeNull();
        lines[1].DimRuns.Should().BeNull();
    }

    [Test]
    public void The_prefix_is_never_dimmed_and_the_two_sides_get_their_own_colors()
    {
        IReadOnlyList<DiffLine> lines = InlineDifferences.Add(
            [new DiffLine("-same old", DiffLineKind.Removed), new DiffLine("+same new", DiffLineKind.Added)], dimBackground: true);

        lines[0].DimRuns![0].Start.Should().Be(1);
        lines[1].DimRuns![0].Start.Should().Be(1);
        lines[0].DimRuns![0].Background.Should().NotBe(lines[1].DimRuns![0].Background);
    }

    [Test]
    public void Without_a_dimmed_background_the_foreground_is_dimmed_instead()
    {
        IReadOnlyList<DiffLine> lines = InlineDifferences.Add(
            [new DiffLine("-same old", DiffLineKind.Removed), new DiffLine("+same new", DiffLineKind.Added)], dimBackground: false);

        lines[0].DimRuns![0].Background.Should().BeNull();
        lines[0].DimRuns![0].Foreground.Should().NotBeNull();
    }

    [Test]
    public void A_block_goes_across_a_few_context_lines_but_not_across_a_hunk()
    {
        DiffLine removed = new("-same old", DiffLineKind.Removed);
        DiffLine added = new("+same new", DiffLineKind.Added);

        InlineDifferences.Add([removed, new DiffLine(" gap", DiffLineKind.Context), added], dimBackground: true)[2].DimRuns
            .Should().NotBeNull();
        InlineDifferences.Add([removed, new DiffLine("@@ -1 +1 @@", DiffLineKind.Hunk), added], dimBackground: true)[2].DimRuns
            .Should().BeNull();
    }

    [Test]
    public void A_moved_line_has_no_line_it_replaces()
    {
        IReadOnlyList<DiffLine> lines = InlineDifferences.Add(
        [
            new DiffLine("-same old", DiffLineKind.Removed) { IsMovedLine = true },
            new DiffLine("+same new", DiffLineKind.Added),
        ], dimBackground: true);

        lines[1].DimRuns.Should().BeNull();
    }

    [Test]
    public void The_lines_are_paired_by_their_likeness()
    {
        IReadOnlyList<DiffLine> lines = InlineDifferences.Add(
        [
            new DiffLine("-alpha one", DiffLineKind.Removed),
            new DiffLine("-beta two", DiffLineKind.Removed),
            new DiffLine("+beta 2", DiffLineKind.Added),
        ], dimBackground: true);

        lines[0].DimRuns.Should().BeNull();
        Dimmed(lines[1]).Should().Contain("beta ");
        Dimmed(lines[2]).Should().Contain("beta ");
    }

    [Test]
    public void A_git_word_diff_gets_nothing()
    {
        IReadOnlyList<DiffLine> lines =
            [new DiffLine("-same old", DiffLineKind.Removed), new DiffLine("+same new", DiffLineKind.Added)];
        DiffOptions word = new() { Appearance = GitCommands.Settings.DiffDisplayAppearance.GitWordDiff };

        InlineDifferences.Add(lines, word).Should().BeSameAs(lines);
    }

    private static IEnumerable<string> Dimmed(DiffLine line)
        => line.DimRuns!.Select(run => line.Text.Substring(run.Start, run.Length));
}
