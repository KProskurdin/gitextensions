using AwesomeAssertions;
using GitCommands;
using NUnit.Framework;

namespace GitCommandsTests;

internal sealed class XplatGitDiscoveryTests
{
    private const string MacCommandLineToolsGit = "/Library/Developer/CommandLineTools/usr/bin/git";

    [Test]
    public void Probe_should_report_found_for_a_supported_version()
    {
        GitDiscoveryResult result = XplatGitDiscovery.Probe("/usr/bin/git", _ => "git version 2.46.0");

        result.Status.Should().Be(GitDiscoveryStatus.Found);
        result.Command.Should().Be("/usr/bin/git");
        result.Version?.ToString().Should().Be("2.46.0");
    }

    [Test]
    public void Probe_should_report_too_old_below_the_supported_minimum()
    {
        GitDiscoveryResult result = XplatGitDiscovery.Probe("/usr/bin/git", _ => "git version 2.20.1");

        result.Status.Should().Be(GitDiscoveryStatus.TooOld);
        result.Version?.ToString().Should().Be("2.20.1");
    }

    [Test]
    public void Probe_should_report_not_found_when_the_command_fails_to_run()
    {
        GitDiscoveryResult result =
            XplatGitDiscovery.Probe("/opt/git", _ => throw new InvalidOperationException("no such file"));

        result.Status.Should().Be(GitDiscoveryStatus.NotFound);
        result.Command.Should().Be("/opt/git");
    }

    [Test]
    public void Probe_should_report_not_found_when_the_output_is_not_git()
    {
        GitDiscoveryResult result = XplatGitDiscovery.Probe("/usr/bin/git", _ => "hello");

        result.Status.Should().Be(GitDiscoveryStatus.NotFound);
    }

    [Test]
    public void GetCandidates_should_list_path_entries_before_known_locations()
    {
        HashSet<string> files = ["/a/git", "/usr/local/bin/git"];

        XplatGitDiscovery.GetCandidates(isMacOS: false, files.Contains, "/a:/b").Should()
            .Equal("/a/git", "/usr/local/bin/git");
    }

    [Test]
    public void GetCandidates_should_not_repeat_a_directory_listed_twice()
    {
        HashSet<string> files = ["/usr/local/bin/git"];

        XplatGitDiscovery.GetCandidates(isMacOS: false, files.Contains, "/usr/local/bin:/usr/local/bin/").Should()
            .Equal("/usr/local/bin/git");
    }

    [Test]
    public void GetCandidates_should_skip_system_git_on_macos_without_command_line_tools()
    {
        HashSet<string> files = ["/usr/bin/git"];

        XplatGitDiscovery.GetCandidates(isMacOS: true, files.Contains, "/usr/bin").Should().BeEmpty();
    }

    [Test]
    public void GetCandidates_should_keep_system_git_on_macos_with_command_line_tools()
    {
        HashSet<string> files = ["/usr/bin/git", MacCommandLineToolsGit];

        XplatGitDiscovery.GetCandidates(isMacOS: true, files.Contains, "/usr/bin").Should()
            .Equal("/usr/bin/git", MacCommandLineToolsGit);
    }

    [Test]
    public void GetCandidates_should_return_nothing_when_no_git_exists()
    {
        XplatGitDiscovery.GetCandidates(isMacOS: false, _ => false, "/usr/bin").Should().BeEmpty();
    }
}
