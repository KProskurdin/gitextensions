using AwesomeAssertions;
using GitCommands.Git.Gpg;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitInfoTests
{
    private static readonly ObjectId _commit = ObjectId.Parse("1234567890123456789012345678901234567890");
    private static readonly ObjectId _parent = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");

    [Test]
    public void Parse_should_read_links_underlines_entities_and_lines()
    {
        IReadOnlyList<IReadOnlyList<InfoSpan>> lines = CommitInfoText.Parse(
            "<u>v1</u>: a &lt;b&gt;\r\nsee <a href='gitext://gotocommit/abc'>abc</a> now\n\nend");

        lines.Should().HaveCount(4);
        lines[0].Should().Equal(new InfoSpan("v1", Underline: true), new InfoSpan(": a <b>"));
        lines[1].Should().Equal(new InfoSpan("see "), new InfoSpan("abc", "gitext://gotocommit/abc"), new InfoSpan(" now"));
        lines[2].Should().BeEmpty();
        lines[3].Should().Equal(new InfoSpan("end"));
    }

    [Test]
    public void Header_should_have_upstreams_rows_with_links_to_the_author_and_the_parents()
    {
        DateTime date = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Local);
        CommitData data = new(_commit, [_parent], "Ann <ann@example.com>", date, "Ann <ann@example.com>", date, "msg")
        {
            ChildIds = [ObjectId.WorkTreeId, ObjectId.IndexId],
        };

        IReadOnlyList<InfoHeaderRow> rows = CommitInfoText.Header(data);

        rows.Select(row => row.Label).Should().Equal("Author:", "Date:", "Commit hash:", "Children:", "Parent:");
        rows[0].Value.Should().Equal(new InfoSpan("Ann <ann@example.com>", "mailto:ann@example.com"));
        CommitInfoText.PlainText(rows[2].Value).Should().Be(_commit.ToString());
        rows[3].Value.Where(span => span.Link is not null).Select(span => span.Text)
            .Should().Equal("Working directory", "Commit index");
        rows[4].Value.Should().Equal(new InfoSpan(_parent.ToShortString(), $"gitext://gotocommit/{_parent}"));
    }

    [Test]
    public void Header_should_show_the_committer_and_both_dates_when_they_differ()
    {
        CommitData data = new(_commit, [], "Ann <ann@example.com>", new DateTime(2026, 1, 1), "Bob <bob@example.com>",
            new DateTime(2026, 1, 2), "msg");

        CommitInfoText.Header(data).Select(row => row.Label)
            .Should().Equal("Author:", "Author date:", "Committer:", "Commit date:", "Commit hash:");
    }

    [Test]
    public void HeaderPlainText_should_leave_out_the_parents()
    {
        CommitData data = new(_commit, [_parent], "Ann <ann@example.com>", DateTime.Now, "Ann <ann@example.com>",
            DateTime.Now, "msg");

        string text = CommitInfoText.HeaderPlainText(CommitInfoText.Header(data));

        text.Should().StartWith("Author: Ann <ann@example.com>").And.Contain($"Commit hash: {_commit}")
            .And.NotContain("Parent");
    }

    [Test]
    public void TryParseCommand_should_read_internal_links_only()
    {
        CommitInfoText.TryParseCommand("gitext://gotobranch/feature/x", out string command, out string data)
            .Should().BeTrue();
        (command, data).Should().Be(("gotobranch", "feature/x"));
        CommitInfoText.TryParseCommand("https://example.com/a", out _, out _).Should().BeFalse();
        CommitInfoText.TryParseCommand("mailto:a@b.c", out _, out _).Should().BeFalse();
    }

    [Test]
    public void BranchComparer_should_put_the_current_branch_then_prioritized_ones_first()
    {
        List<string> branches =
        [
            "remotes/origin/feature", "feature", "remotes/upstream/main", "remotes/origin/main", "main", "current"
        ];

        branches.Sort(new CommitInfoReader.BranchComparer(branches, "current",
            new RefSorting(PrioritizedBranchNames: "main", PrioritizedRemoteNames: "origin|upstream")));

        branches.Should().Equal("current", "main", "remotes/origin/main", "remotes/upstream/main", "feature",
            "remotes/origin/feature");
    }

    [Test]
    public void Display_should_show_upstreams_texts_and_images_for_the_signatures()
    {
        CommitInfoReader.Display(null).Should().Be(new GpgDisplay(null, "Commit is not signed", null, null));
        CommitInfoReader.Display(new GpgInfo(CommitStatus.GoodSignature, "gpg: Good\r\n", TagStatus.NoTag, null))
            .Should().Be(new GpgDisplay("CommitSignatureOk", "gpg: Good", null, null));
        CommitInfoReader.Display(new GpgInfo(CommitStatus.NoSignature, "", TagStatus.TagNotSigned, "error"))
            .Should().Be(new GpgDisplay(null, "Commit is not signed", null, "Tag is not signed"));
        CommitInfoReader.Display(new GpgInfo(CommitStatus.MissingPublicKey, "no key", TagStatus.Many, "a\nb"))
            .Should().Be(new GpgDisplay("CommitSignatureWarning", "no key", "TagMany", "a\nb"));
    }
}
