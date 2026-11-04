using AwesomeAssertions;
using GitCommands.Git;
using GitExtensions.Xplat.Core.Platform;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class EnvironmentInformationTests
{
    [Test]
    public void Information_should_be_upstreams_lines()
    {
        string[] lines = EnvironmentInformation.Information("6.0.0", "abc123", dirty: true, "2.53.0", scale: 1.5,
            portable: false).Split(Environment.NewLine);

        lines[0].Should().Be("- Git Extensions 6.0.0");
        lines[1].Should().Be("- Build abc123 (Dirty)");
        lines[2].Should().Be("- Git 2.53.0");
        lines[5].Should().Be("- DPI 144dpi (150% scaling)");
        lines[6].Should().Be("- Portable: False");
    }

    [Test]
    public void Information_should_say_no_scaling_at_96_dpi()
    {
        EnvironmentInformation.Information("6.0.0", "abc123", dirty: false, "2.53.0", scale: 1, portable: true)
            .Should().Contain("- Build abc123" + Environment.NewLine).And.Contain("- DPI 96dpi (no scaling)");
    }

    [TestCase(null, "- (minimum: 2.43.0, recommended: 2.53.0)")]
    [TestCase("2.40.0", "2.40.0 (minimum: 2.43.0, please update!)")]
    [TestCase("2.45.1", "2.45.1 (recommended: 2.53.0 or later)")]
    [TestCase("2.54.0", "2.54.0")]
    public void GitVersionInfo_should_note_an_old_version(string? version, string expected)
    {
        EnvironmentInformation.GitVersionInfo(version, new GitVersion("2.43.0"), new GitVersion("2.53.0"))
            .Should().Be(expected);
    }

    [Test]
    public void Contributors_List_should_be_every_name_of_the_four_lists()
    {
        Contributors.List("Ann, Bob", "Cid,\nDee,", "Eve", "Fay").Should().Equal("Ann", "Bob", "Cid", "Dee", "Eve", "Fay");
    }

    [Test]
    public void Contributors_DevelopersText_should_be_the_team_and_the_contributors_on_one_line_each()
    {
        Contributors.DevelopersText("Ann, Bob", "Cid,\r\nDee", "Team", "Contributors")
            .Should().Be("Team:\r\nAnn, Bob\r\n\r\nContributors:\r\nCid, Dee");
    }
}
