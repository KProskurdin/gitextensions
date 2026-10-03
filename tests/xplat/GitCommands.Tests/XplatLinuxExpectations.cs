using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using NUnit.Framework;

namespace GitCommandsTests;

/// <summary>
///  Linux (and macOS) expectations for behavior that upstream's tests only assert for Windows. The upstream tests remain
///  Windows-only (see XplatPlatformSkips.cs); these are the matching Linux assertions, so the behavior stays covered on both.
/// </summary>
// Mirrors: none (cross-cutting; the upstream tests it covers are PathEqualityComparerTests, GitBranchNameNormaliserTest, GitModuleTests, CommitMessageManagerTests)
internal sealed class XplatLinuxExpectationsTests
{
    private const string PosixPlatforms = "Linux,MacOsX";

    [Test]
    public void Equals_should_treat_trailing_separator_as_same_path()
    {
        PathEqualityComparer comparer = new();

        comparer.Equals("/work/repo/", "/work/repo").Should().BeTrue();
    }

    [Test]
    [Platform(Include = PosixPlatforms)]
    public void Equals_should_be_case_sensitive_on_posix()
    {
        PathEqualityComparer comparer = new();

        comparer.Equals("/work/Repo", "/work/repo").Should().BeFalse();
    }

    [Test]
    [Platform(Include = PosixPlatforms)]
    public void Normalise_rule04_should_keep_pipe_on_posix()
    {
        // '|' is a valid filename character on Linux and macOS, so Rule04 keeps it there.
        GitBranchNameNormaliser.Rule04("test|test", new GitBranchNameOptions("_")).Should().Be("test|test");
    }

    [Test]
    public void FormatCommitMessage_should_end_lines_with_platform_newline()
    {
        // Commit message files use the platform's line ending: CRLF on Windows, LF on Linux.
        CommitMessageManager.FormatCommitMessage("Test message", usingCommitTemplate: false, ensureCommitMessageSecondLineEmpty: false)
            .Should().Be("Test message" + Environment.NewLine);
    }
}
