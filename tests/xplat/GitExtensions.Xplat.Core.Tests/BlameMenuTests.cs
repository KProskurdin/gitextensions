using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class BlameMenuTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";

    private const string Porcelain =
        Hash + " 7 3 1\n" +
        "author Ann\n" +
        "author-mail <ann@example.com>\n" +
        "author-time 1700000000\n" +
        "author-tz +0100\n" +
        "committer Bob\n" +
        "committer-mail <bob@example.com>\n" +
        "committer-time 1700086400\n" +
        "committer-tz +0200\n" +
        "summary the subject\n" +
        "previous fedcba9876543210fedcba9876543210fedcba98 old.txt\n" +
        "filename old.txt\n" +
        "\ttext\n";

    [Test]
    public void The_texts_are_upstreams()
    {
        string control = File.ReadAllText(UpstreamBlameControl());

        control.Should().Contain(BlameMenu.BlameActualPreviousRevision);
        control.Should().Contain(BlameMenu.BlameVisiblePreviousRevision);
    }

    [Test]
    public void Parse_should_give_a_line_its_whole_commit_and_origin_line()
    {
        BlameLine line = BlameParser.Parse(Porcelain).Single();

        line.LineNumber.Should().Be(3);
        line.OriginLineNumber.Should().Be(7);
        line.Commit.Should().NotBeNull();
        line.Commit!.ObjectId.ToString().Should().Be(Hash);
        line.Commit.Summary.Should().Be("the subject");
        line.Commit.Committer.Should().Be("Bob");
        line.Commit.FileName.Should().Be("old.txt");
        line.Commit.CommitterTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1700086400).LocalDateTime);
        line.Commit.ToString().Should().Contain("Committer: Bob").And.Contain("Summary: the subject");
    }

    [Test]
    public void The_real_parent_is_taken_when_the_grid_shows_it()
    {
        BlamePreviousRevision previous = BlameMenu.PreviousRevision(["visible"], ["actual", "second"], _ => true);

        previous.Text.Should().Be(BlameMenu.BlameActualPreviousRevision);
        previous.Parents.Should().Equal("actual", "second");
    }

    [Test]
    public void The_grids_rewritten_parent_is_taken_when_the_grid_does_not_show_the_real_one()
    {
        BlamePreviousRevision previous = BlameMenu.PreviousRevision(["visible"], ["actual"], hash => hash == "visible");

        previous.Text.Should().Be(BlameMenu.BlameVisiblePreviousRevision);
        previous.Parents.Should().Equal("visible");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void No_parent_in_the_grid_disables_the_item(bool rootCommit)
    {
        BlamePreviousRevision previous = BlameMenu.PreviousRevision(
            rootCommit ? null : ["hidden"], rootCommit ? [] : ["hidden"], _ => false);

        previous.Text.Should().Be(BlameMenu.BlameVisiblePreviousRevision);
        previous.Parents.Should().BeNull();
    }

    private static string UpstreamBlameControl([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "..",
            "src", "app", "GitUI", "UserControls", "BlameControl.cs"));
}
