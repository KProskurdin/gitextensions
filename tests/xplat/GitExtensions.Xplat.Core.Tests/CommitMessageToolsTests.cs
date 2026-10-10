using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class CommitMessageToolsTests
{
    // The cases of upstream's FormCommitTests (ConventionalCommit_keyword_is_prefixed_*).
    [TestCase("", 0, false, "feat: ", 6)]
    [TestCase("text", 3, false, "feat: text", 9)]
    [TestCase("", 0, true, "feat(): ", 5)]
    [TestCase("text", 3, true, "feat(): text", 5)]
    [TestCase("fix: ", 0, false, "feat: ", 6)]
    [TestCase("fix: text", 3, false, "feat: text", 6)]
    [TestCase("fix: ", 0, true, "feat(): ", 5)]
    [TestCase("fix: text", 3, true, "feat(): text", 5)]
    [TestCase("fix(scope): ", 0, true, "feat(scope): ", 13)]
    [TestCase("fix(scope): text", 14, true, "feat(scope): text", 15)]
    public void ApplyType_should_prefix_or_replace_the_type_as_upstream(string message, int caret, bool scope,
        string expected, int expectedCaret)
    {
        (string result, int resultCaret) =
            ConventionalCommits.ApplyType(message, caret, ConventionalCommits.Feat, scope);

        result.Should().Be(expected);
        resultCaret.Should().Be(expectedCaret);
    }

    [Test]
    public void ApplyType_should_change_only_the_subject_line()
    {
        ConventionalCommits.ApplyType("fix: subject\n\nbody", 0, "docs", insertScope: false).Message
            .Should().Be("docs: subject\n\nbody");
    }

    [Test]
    public void AppendFooter_should_add_a_line_and_keep_the_caret_only_for_skip_ci()
    {
        string newLine = Environment.NewLine;
        ConventionalCommits.AppendFooter("subject", 3, "Reviewed-by: ").Should()
            .Be(($"subject{newLine}Reviewed-by: ", $"subject{newLine}Reviewed-by: ".Length));
        ConventionalCommits.AppendFooter("subject", 3, ConventionalCommits.SkipCi).Caret.Should().Be(3);
    }

    [Test]
    public void Questions_should_ask_about_each_failed_check_in_upstreams_order()
    {
        CommitValidationOptions options = new(MaxFirstLineLength: 5, MaxLineLength: 8, SecondLineMustBeEmpty: true,
            RegEx: "^JIRA-");

        CommitMessageValidation.Questions("a long subject\nsecond\nthird", options).Should().Equal(
            CommitMessageValidation.FirstLineTooLong,
            CommitMessageValidation.LineTooLong("a long subject"),
            CommitMessageValidation.SecondLineNotEmpty,
            CommitMessageValidation.RegExNotMatched);
    }

    [Test]
    public void Questions_should_skip_the_regex_for_fixups_and_a_broken_pattern()
    {
        CommitMessageValidation.Questions("fixup! x", new CommitValidationOptions(RegEx: "^JIRA-")).Should().BeEmpty();
        CommitMessageValidation.Questions("x", new CommitValidationOptions(RegEx: "(")).Should().BeEmpty();
        CommitMessageValidation.Questions("JIRA-1 x", new CommitValidationOptions(RegEx: "^JIRA-")).Should().BeEmpty();
    }

    [Test]
    public void Format_should_move_text_off_the_second_line_with_a_bullet()
    {
        (string message, int caret) = CommitMessageValidation.Format("subject\nb", 9,
            new CommitValidationOptions(SecondLineMustBeEmpty: true, IndentAfterFirstLine: true));

        message.Should().Be("subject\n\n - b");
        caret.Should().Be(13);
    }

    [Test]
    public void Format_should_wrap_body_lines_but_not_the_subject()
    {
        string subject = "a subject longer than ten";
        (string message, _) = CommitMessageValidation.Format($"{subject}\n\none two three four", 0,
            new CommitValidationOptions(MaxLineLength: 10, SecondLineMustBeEmpty: true, AutoWrap: true));

        // Upstream's WordWrapper keeps a line strictly under the limit.
        message.Should().Be($"{subject}\n\none two\nthree\nfour");
    }

    [Test]
    public void WrapBody_should_wrap_at_72_without_a_limit()
    {
        string body = string.Join(' ', Enumerable.Repeat("word", 20));

        CommitMessageValidation.WrapBody($"subject\n{body}", 0).Split('\n').Skip(1)
            .Should().OnlyContain(line => line.Length < 72);
    }
}
