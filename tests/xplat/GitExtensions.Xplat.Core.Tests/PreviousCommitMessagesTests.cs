using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PreviousCommitMessagesTests
{
    [Test]
    public void Merge_should_put_the_last_commit_message_first_and_keep_the_count()
    {
        PreviousCommitMessages.Merge(["c", "b", "a"], "mine", maxCount: 3).Should().Equal("mine", "c", "b");
        PreviousCommitMessages.Merge(["c", "b"], "mine", maxCount: 3).Should().Equal("mine", "c", "b");
    }

    [Test]
    public void Merge_should_not_repeat_a_listed_or_empty_last_message()
    {
        PreviousCommitMessages.Merge(["c", "b"], "b", maxCount: 3).Should().Equal("c", "b");
        PreviousCommitMessages.Merge(["c", "b"], "  ", maxCount: 3).Should().Equal("c", "b");
    }

    [Test]
    public void Label_should_be_the_first_line_shortened_to_72_characters()
    {
        PreviousCommitMessages.Label("subject\r\n\r\nbody").Should().Be("subject");

        string label = PreviousCommitMessages.Label(new string('x', 100));

        label.Length.Should().Be(72);
        label.Should().EndWith("...");
    }

    [Test]
    public void AuthorPattern_should_match_exactly_the_configured_name_and_email()
    {
        PreviousCommitMessages.AuthorPattern("A. Name", "a+b@example.com")
            .Should().Be(@"^A\.\ Name <a\+b@example\.com>$");
    }
}
