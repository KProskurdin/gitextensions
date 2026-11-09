using AwesomeAssertions;
using GitExtensions.Xplat.Core.Spelling;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitSpellCheckerTests
{
    private const string English = "en-US";

    private static readonly string _folder = Path.Combine(AppContext.BaseDirectory, "Dictionaries");

    // The user word list stays off: it lives in the user's application data folder.
    private static CommitSpellChecker NewChecker() => new(_folder, enableUserFile: false);

    [Test]
    public void Misspelled_should_find_the_words_the_dictionary_lacks()
    {
        const string text = "This is a tset of speling.";

        NewChecker().Misspelled(text, English).Select(range => text.Substring(range.Start, range.Length)).Should()
            .Equal("tset", "speling");
    }

    [Test]
    public void Misspelled_should_pass_over_capitals_words_starting_with_a_digit_and_the_auto_completion_words()
    {
        CommitSpellChecker checker = NewChecker();
        checker.AddKnownWords(["CommitSpellChecker"]);

        const string text = "Fix HTTPX in 2xq of CommitSpellChecker";
        checker.Misspelled(text, English).Select(range => text.Substring(range.Start, range.Length)).Should().BeEmpty();
    }

    [TestCase("None")]
    [TestCase("none")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("xx-XX")]
    public void A_dictionary_that_is_off_or_missing_should_check_nothing(string? dictionary)
    {
        CommitSpellChecker checker = NewChecker();

        checker.IsOff(dictionary).Should().BeTrue();
        checker.Misspelled("tset speling", dictionary).Should().BeEmpty();
        checker.SuggestionsAt("tset", 1, dictionary).Should().BeNull();
    }

    [Test]
    public void Misspelled_should_leave_a_long_text_unchecked_as_upstream()
        => NewChecker().Misspelled(string.Concat(Enumerable.Repeat("tset ", 1000)), English).Should().BeEmpty();

    [Test]
    public void SuggestionsAt_should_give_the_word_under_the_position_and_upstreams_five_suggestions()
    {
        const string text = "Fix speling here";
        CommitSpellChecker checker = NewChecker();

        SpellingSuggestions suggestions = checker.SuggestionsAt(text, 6, English)!;

        suggestions.Word.Should().Be("speling");
        suggestions.Range.Should().Be(new TextRange(4, 7));
        suggestions.Suggestions.Should().Contain("spelling").And.HaveCountLessThanOrEqualTo(5);
        checker.SuggestionsAt(text, 1, English).Should().BeNull("\"Fix\" is spelled right");
    }

    [Test]
    public void Added_and_ignored_words_should_no_longer_be_misspelled()
    {
        CommitSpellChecker checker = NewChecker();

        checker.AddToDictionary("tset", English);
        checker.Ignore("speling");

        checker.Misspelled("tset speling", English).Should().BeEmpty();
        checker.SuggestionsAt("speling", 1, English).Should().BeNull();
    }

    [Test]
    public void Another_dictionary_should_be_read_when_chosen()
    {
        CommitSpellChecker checker = NewChecker();

        checker.Misspelled("Haus", English).Should().ContainSingle();
        checker.Misspelled("Haus", "de-DE").Should().BeEmpty();
    }

    [Test]
    public void Dictionaries_should_list_the_dic_files_by_name()
    {
        CommitSpellChecker.Dictionaries(_folder).Should().Equal("de-DE", "en-US");
        CommitSpellChecker.Dictionaries(Path.Combine(_folder, "missing")).Should().BeEmpty();
    }

    [TestCase("tset is", 0, 4, "is")]
    [TestCase("a tset b", 2, 4, "a  b")]
    public void RemoveWord_should_take_the_word_and_the_space_after_it_at_the_start(string text, int start, int length,
        string expected)
        => CommitSpellChecker.RemoveWord(text, new TextRange(start, length)).Should().Be(expected);

    [Test]
    public void IllFormedLines_should_mark_past_50_then_the_second_line_then_past_72()
    {
        string subject = new('s', 55);
        string body = new('b', 80);
        string text = $"{subject}\nnot empty\r\n{body}\nshort";

        CommitSpellChecker.IllFormedLines(text).Should().Equal(
            new TextRange(50, 5),
            new TextRange(56, 9),
            new TextRange(67 + 72, 8));
        CommitSpellChecker.IllFormedLines("fine\n\nalso fine").Should().BeEmpty();
    }

    [TestCase("\nbody", true)]
    [TestCase("\r\nbody", true)]
    [TestCase("\n", false)]
    [TestCase("subject\n", false)]
    public void MarksEmptySubject_should_be_upstreams_first_row_mark(string text, bool expected)
        => CommitSpellChecker.MarksEmptySubject(text).Should().Be(expected);
}
