using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitUIPluginInterfaces;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class RevisionTooltipsTests
{
    [Test]
    public void Author_and_date_tooltips_should_name_one_person_once_and_two_people_on_two_lines()
    {
        GitRevision same = Revision();
        GitRevision different = Revision();
        different.Committer = "Bob";
        different.CommitterEmail = "bob@example.com";
        different.CommitUnixTime = same.AuthorUnixTime + 3600;

        RevisionTooltip one = RevisionTooltips.For(same, []);
        RevisionTooltip two = RevisionTooltips.For(different, []);

        one.Author.Should().Be("Ann <ann@example.com> authored and committed");
        one.Date.Should().Be($"{same.AuthorDate:g} Ann authored and committed");
        two.Author.Should().Be("Ann <ann@example.com> authored\nBob <bob@example.com> committed");
        two.Date.Should().Be($"{different.AuthorDate:g} Ann authored\n{different.CommitDate:g} Bob committed");
        one.Hash.Should().Be(same.Guid);
    }

    [Test]
    public void Message_tooltip_should_be_shown_for_a_long_message_or_refs_only()
    {
        GitRevision plain = Revision();
        GitRevision multiLine = Revision();
        multiLine.Body = "Subject\n\nThe body.";
        multiLine.HasMultiLineMessage = true;

        RevisionTooltips.For(plain, [new RefLabel("HEAD", RefKind.Head)]).Message.Should().BeNull();
        RevisionTooltips.For(multiLine, []).Message.Should().Be("Subject\n\nThe body.");
        RevisionTooltips.For(plain,
            [
                new RefLabel("v1", RefKind.Tag), new RefLabel("origin/main", RefKind.RemoteBranch),
                new RefLabel("main", RefKind.Branch), new RefLabel("HEAD", RefKind.Head),
            ]).Message.Should()
            .Be($"Subject{Environment.NewLine}{Environment.NewLine}[main]{Environment.NewLine}[origin/main]" +
                $"{Environment.NewLine}[v1]{Environment.NewLine}");
    }

    [Test]
    public void Message_tooltip_should_say_when_the_body_was_not_loaded()
    {
        GitRevision revision = Revision();
        revision.HasMultiLineMessage = true;

        RevisionTooltips.For(revision, []).Message.Should().Be("Subject" + RevisionTooltips.BodyNotLoaded);
    }

    [Test]
    public void The_texts_are_upstreams()
    {
        string strings = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "TranslatedStrings.cs"));
        string date = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "UserControls",
            "RevisionGrid", "Columns", "DateColumnProvider.cs"));

        strings.Should().Contain($"new(\"{RevisionTooltips.Authored}\")")
            .And.Contain($"new(\"{RevisionTooltips.Committed}\")")
            .And.Contain($"new(\"{RevisionTooltips.AuthoredAndCommitted}\")")
            .And.Contain(RevisionTooltips.BodyNotLoaded.Replace("\n", "\\n"));
        date.Should().Contain("{revision.Author} authored and committed").And.Contain("{revision.Committer} committed");
    }

    private static GitRevision Revision()
        => new(ObjectId.Random())
        {
            Author = "Ann",
            AuthorEmail = "ann@example.com",
            Committer = "Ann",
            CommitterEmail = "ann@example.com",
            AuthorUnixTime = 1_700_000_000,
            CommitUnixTime = 1_700_000_000,
            Subject = "Subject",
        };

    private static string RepositoryRoot([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
