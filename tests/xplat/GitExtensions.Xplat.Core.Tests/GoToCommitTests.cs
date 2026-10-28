using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GoToCommitTests
{
    private const string Head = "1234567890123456789012345678901234567890";
    private const string First = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Second = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Third = "cccccccccccccccccccccccccccccccccccccccc";

    [Test]
    public void MergeBaseArguments_should_compare_one_commit_with_HEAD()
        => Arguments([First]).Should().Be($"merge-base {Head} {First}");

    [Test]
    public void MergeBaseArguments_should_take_an_artificial_commit_as_HEAD()
    {
        Arguments([ArtificialCommits.WorkTreeHash]).Should().Be($"merge-base {Head} {Head}");
        Arguments([ArtificialCommits.IndexHash, First, Second]).Should().Be($"merge-base --octopus {First} {Second}");
    }

    [Test]
    public void MergeBaseArguments_should_use_octopus_for_more_than_two_commits()
    {
        Arguments([First, Second]).Should().Be($"merge-base {First} {Second}");
        Arguments([First, Second, Third]).Should().Be($"merge-base --octopus {First} {Second} {Third}");
    }

    [Test]
    public void MergeBaseArguments_should_be_null_without_HEAD_or_a_selection()
    {
        GoToCommit.MergeBaseArguments([First], default).Should().BeNull();
        GoToCommit.MergeBaseArguments([], ObjectId.Parse(Head)).Should().BeNull();
    }

    private static string? Arguments(IReadOnlyList<string> selected)
        => GoToCommit.MergeBaseArguments(selected, ObjectId.Parse(Head))?.ToString();
}
