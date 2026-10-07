using AwesomeAssertions;
using GitCommands.Settings;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffOptionsTests
{
    [Test]
    public void Arguments_should_be_upstreams_file_viewer_arguments()
    {
        new DiffOptions().Arguments.Should().Equal("--unified=3");
        new DiffOptions(IgnoreWhitespaceKind.Eol, ContextLines: 0).Arguments.Should()
            .Equal("--ignore-space-at-eol", "--unified=0");
        new DiffOptions(IgnoreWhitespaceKind.Change).Arguments.Should().Equal("--ignore-space-change", "--unified=3");
        new DiffOptions(IgnoreWhitespaceKind.AllSpace, ShowEntireFile: true).Arguments.Should()
            .Equal("--ignore-all-space", "--inter-hunk-context=9000", "--unified=9000");
    }

    [Test]
    public void Toggles_should_follow_upstreams_buttons()
    {
        DiffOptions options = new();

        options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Eol).IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.Eol);
        options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Eol).ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Eol)
            .IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.None);
        options.ToggleIgnoreWhitespace(IgnoreWhitespaceKind.Eol).ToggleIgnoreWhitespace(IgnoreWhitespaceKind.AllSpace)
            .IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.AllSpace);
        options.WithMoreContext().ContextLines.Should().Be(4);
        new DiffOptions(ContextLines: 0).WithLessContext().ContextLines.Should().Be(0);
        options.ToggleEntireFile().ShowEntireFile.Should().BeTrue();
    }

    [Test]
    public void A_new_view_keeps_this_runs_whitespace_choice_only_when_remembered()
    {
        InMemoryAppPreferences preferences = new();
        preferences.SetDiffOptions(new DiffOptions(IgnoreWhitespaceKind.Change, ContextLines: 5, ShowEntireFile: true));

        // Upstream's defaults: whitespace remembered, entire file and context lines not.
        preferences.InitialDiffOptions().Should()
            .Be(new DiffOptions(IgnoreWhitespaceKind.Change, ContextLines: 3, ShowEntireFile: false));

        preferences.RememberIgnoreWhiteSpacePreference = false;
        preferences.InitialDiffOptions().IgnoreWhitespace.Should().Be(IgnoreWhitespaceKind.None);
    }

    [Test]
    public void Saved_defaults_and_remembered_context_lines_apply_to_new_views()
    {
        InMemoryAppPreferences preferences = new()
        {
            RememberIgnoreWhiteSpacePreference = false, RememberNumberOfContextLines = true,
        };
        preferences.SetDiffOptions(new DiffOptions(IgnoreWhitespaceKind.Eol, ContextLines: 7, ShowEntireFile: true));
        preferences.SaveDiffOptionsAsDefault();
        preferences.SetDiffOptions(new DiffOptions(IgnoreWhitespaceKind.AllSpace, ContextLines: 7));

        preferences.InitialDiffOptions().Should()
            .Be(new DiffOptions(IgnoreWhitespaceKind.Eol, ContextLines: 7, ShowEntireFile: true));
    }
}
