using AwesomeAssertions;
using GitExtensions.Xplat.Core.Editing;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class EditorHighlightingTests
{
    [TestCase("/repo/.git/rebase-merge/git-rebase-todo", EditorSyntax.RebaseTodo)]
    [TestCase("/repo/.git/COMMIT_EDITMSG", EditorSyntax.CommitMessage)]
    [TestCase("/repo/readme.md", EditorSyntax.File)]
    public void SyntaxOf_should_choose_upstreams_strategy_by_the_file_name(string path, EditorSyntax expected)
        => EditorHighlighting.SyntaxOf(path).Should().Be(expected);

    [TestCase(null, '#')]
    [TestCase("auto", '#')]
    [TestCase(";", ';')]
    public void CommentChar_should_be_the_setting_when_it_is_one_character(string? setting, char expected)
        => EditorHighlighting.CommentChar(setting).Should().Be(expected);

    [Test]
    public void A_todo_lines_command_and_commit_take_the_commands_color_and_comments_their_own()
    {
        string[] lines =
        [
            "pick 1a2b3c4 first",
            "f -C 1234abcd fixed",
            "drop deadbeef gone",
            "  # a comment",
            "pick 12 too short",
            "pickle 1234567 not a command",
            "exec make test",
        ];

        IReadOnlyList<IReadOnlyList<EditorToken>> tokens =
            EditorHighlighting.Tokens(EditorSyntax.RebaseTodo, lines, '#');

        tokens[0].Should().Equal(new EditorToken(0, 5, EditorTokenKind.Pick), new EditorToken(5, 7, EditorTokenKind.Pick));
        tokens[1].Should().Equal(new EditorToken(0, 5, EditorTokenKind.Fixup), new EditorToken(5, 8, EditorTokenKind.Fixup));
        tokens[2].Should().Equal(new EditorToken(0, 5, EditorTokenKind.Drop), new EditorToken(5, 8, EditorTokenKind.Drop));
        tokens[3].Should().Equal(new EditorToken(0, lines[3].Length, EditorTokenKind.Comment));
        tokens[4].Should().BeEmpty("an id has at least four characters");
        tokens[5].Should().BeEmpty();
        tokens[6].Should().BeEmpty("upstream colors exec only before a hex id");
    }

    [Test]
    public void A_commit_messages_summary_is_bold_and_its_comments_are_comments()
    {
        string[] lines = ["; comment first", "", "Fix the thing", "", "Body", "; more"];

        IReadOnlyList<IReadOnlyList<EditorToken>> tokens =
            EditorHighlighting.Tokens(EditorSyntax.CommitMessage, lines, ';');

        tokens.Select(line => line.SingleOrDefault()?.Kind).Should().Equal(
            EditorTokenKind.Comment, null, EditorTokenKind.Summary, null, null, EditorTokenKind.Comment);
        EditorHighlighting.Tokens(EditorSyntax.File, lines, ';').Should().OnlyContain(line => line.Count == 0);
    }

    [Test]
    public void CommitMessageMarks_should_mark_a_long_summary_and_long_description_lines_after_the_blank_line()
    {
        string summary = new('s', 55);
        string longLine = new('d', 85);
        string[] lines = ["# comment", summary, "", "# another comment", "short body", longLine];

        EditorHighlighting.CommitMessageMarks(lines, '#').Should().Equal(
            new EditorMark(1, 50, 5, EditorHighlighting.SummaryTooLongText),
            new EditorMark(5, 80, 5, EditorHighlighting.LineTooLongText));
    }

    [Test]
    public void CommitMessageMarks_should_mark_a_description_with_no_blank_line_before_it()
    {
        string[] lines = ["Summary", "Body right after"];

        EditorHighlighting.CommitMessageMarks(lines, '#').Should().Equal(
            new EditorMark(1, 0, lines[1].Length, EditorHighlighting.SpacerNeededText));
        EditorHighlighting.CommitMessageMarks(["Summary", "", "Body"], '#').Should().BeEmpty();
        EditorHighlighting.CommitMessageMarks(["# only comments"], '#').Should().BeEmpty();
    }

    [Test]
    public void The_marks_texts_are_upstreams()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", "Editor",
            "CommitMessageHighlightingStrategy.cs"));

        source.Should().Contain($"\"{EditorHighlighting.SummaryTooLongText}\"")
            .And.Contain($"\"{EditorHighlighting.SpacerNeededText}\"")
            .And.Contain($"\"{EditorHighlighting.LineTooLongText}\"")
            .And.Contain($"maxSummaryLength = {EditorHighlighting.MaxSummaryLength};")
            .And.Contain($"maxDescriptionLength = {EditorHighlighting.MaxDescriptionLength};");
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
