using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using GitUI.AutoCompletion;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitMessageAutoCompletionTests
{
    [TestCase("fix Ren", 7, "Ren")]
    [TestCase("fix Ren", 5, "R")]
    [TestCase("fix ", 4, "")]
    [TestCase("see .gitignore", 14, ".gitignore")]
    [TestCase("call(foo.Bar", 12, "Bar")]
    public void WordAtCursor_should_be_the_part_of_the_word_before_the_caret(string text, int caret, string expected)
    {
        CommitMessageAutoCompletion.WordAtCursor(text, caret).Should().Be(expected);
    }

    [Test]
    public void Accept_should_replace_the_word_before_the_caret_and_keep_the_rest()
    {
        (string text, int caret) = CommitMessageAutoCompletion.Accept("fix Ren in x", 7, "RenderWidget");

        text.Should().Be("fix RenderWidget in x");
        caret.Should().Be(16);
    }

    [Test]
    public void Matches_should_take_the_start_or_the_capitals_ignoring_case()
    {
        AutoCompleteWord[] words = [new("RenderWidget"), new("DrawFrame"), new("readme")];

        CommitMessageAutoCompletion.Matches(words, "re").Select(word => word.Word).Should().Equal("RenderWidget", "readme");
        CommitMessageAutoCompletion.Matches(words, "df").Select(word => word.Word).Should().Equal("DrawFrame");
    }
}
