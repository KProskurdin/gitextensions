namespace GitExtensions.Xplat.Core.Editing;

/// <summary>
///  How upstream's editor colors a file it opens for git (<c>FileViewerInternal.SetHighlightingForFile</c>).
/// </summary>
public enum EditorSyntax
{
    /// <summary>
    ///  Any other file: the syntax of its extension.
    /// </summary>
    File,

    /// <summary>
    ///  Upstream's <c>RebaseTodoHighlightingStrategy</c>, for <c>git-rebase-todo</c>.
    /// </summary>
    RebaseTodo,

    /// <summary>
    ///  Upstream's <c>CommitMessageHighlightingStrategy</c>, for <c>COMMIT_EDITMSG</c>.
    /// </summary>
    CommitMessage,
}

/// <summary>
///  What a span of a line is, for its color: a comment, the summary of a message, or a rebase command with its commit.
/// </summary>
public enum EditorTokenKind
{
    Comment,
    Summary,
    Pick,
    Reword,
    Edit,
    Squash,
    Fixup,
    Exec,
    Drop,
}

/// <summary>
///  A span of a line in the color of its kind; the rest of the line has the normal color.
/// </summary>
public sealed record EditorToken(int Start, int Length, EditorTokenKind Kind);

/// <summary>
///  Upstream's red wave under a part of a commit message, with its tooltip (<c>CommitMessageHighlightingStrategy</c>).
/// </summary>
public sealed record EditorMark(int Line, int Start, int Length, string ToolTip);

/// <summary>
///  Upstream's highlighting strategies of git's files (<c>GitHighlightingStrategyBase</c> and its rebase todo and commit
///  message strategies), as tokens per line.
/// </summary>
public static class EditorHighlighting
{
    /// <summary>
    ///  Git's default comment character, which <c>core.commentchar</c> changes.
    /// </summary>
    public const char DefaultCommentChar = '#';

    /// <summary>
    ///  Upstream's <c>maxSummaryLength</c> and <c>maxDescriptionLength</c>.
    /// </summary>
    public const int MaxSummaryLength = 50;

    public const int MaxDescriptionLength = 80;

    // Upstream's markers' tooltips.
    public const string SummaryTooLongText = "Summary line is too long.";
    public const string SpacerNeededText = "There must be a blank line after the summary.";
    public const string LineTooLongText = "Line is too long.";

    private const int MinimumIdLength = 4;

    // Upstream's _commandByFirstChar: a command's letter, its long form, its kind and its options.
    private static readonly Dictionary<char, (string LongForm, EditorTokenKind Kind, string[] Options)> _commands = new()
    {
        ['p'] = ("pick", EditorTokenKind.Pick, []),
        ['r'] = ("reword", EditorTokenKind.Reword, []),
        ['e'] = ("edit", EditorTokenKind.Edit, []),
        ['s'] = ("squash", EditorTokenKind.Squash, []),
        ['f'] = ("fixup", EditorTokenKind.Fixup, ["-C", "-c"]),
        ['x'] = ("exec", EditorTokenKind.Exec, []),
        ['d'] = ("drop", EditorTokenKind.Drop, []),
    };

    private enum State
    {
        Command,
        SpacesAfterCommand,
        Id,
    }

    /// <summary>
    ///  Upstream's <c>SetHighlightingForFile</c>: the strategy for <paramref name="path"/>.
    /// </summary>
    public static EditorSyntax SyntaxOf(string path)
        => path.EndsWith("git-rebase-todo", StringComparison.Ordinal) ? EditorSyntax.RebaseTodo
            : path.EndsWith("COMMIT_EDITMSG", StringComparison.Ordinal) ? EditorSyntax.CommitMessage
            : EditorSyntax.File;

    /// <summary>
    ///  Upstream's comment character from the <c>core.commentchar</c> setting: a single character, else '#' (also for
    ///  "auto", whose character git does not tell the editor).
    /// </summary>
    public static char CommentChar(string? setting) => setting is { Length: 1 } ? setting[0] : DefaultCommentChar;

    /// <summary>
    ///  The tokens of each of <paramref name="lines"/> in <paramref name="syntax"/>; none for <see cref="EditorSyntax.File"/>.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<EditorToken>> Tokens(EditorSyntax syntax, IReadOnlyList<string> lines,
        char commentChar)
    {
        List<IReadOnlyList<EditorToken>> tokens = [];
        bool summarySeen = false;
        foreach (string line in lines)
        {
            if (syntax == EditorSyntax.File)
            {
                tokens.Add([]);
            }
            else if (IsComment(line, commentChar))
            {
                tokens.Add([new EditorToken(0, line.Length, EditorTokenKind.Comment)]);
            }
            else if (syntax == EditorSyntax.RebaseTodo)
            {
                tokens.Add(TodoCommand(line));
            }
            else if (!summarySeen && !string.IsNullOrWhiteSpace(line))
            {
                // Upstream's ColorSummary: the message's first line that is not a comment, in bold.
                summarySeen = true;
                tokens.Add([new EditorToken(0, line.Length, EditorTokenKind.Summary)]);
            }
            else
            {
                tokens.Add([]);
            }
        }

        return tokens;
    }

    /// <summary>
    ///  Upstream's markers of a commit message: the summary past 50 characters, the description's first line when no blank
    ///  line comes before it, and the description's lines past 80 characters.
    /// </summary>
    /// <remarks>
    ///  Upstream looks for the description after the blank line with the condition
    ///  <c>descriptionStartLineNumber != -1</c>, which never holds there, so its long lines are marked only when the summary
    ///  has no blank line after it. This takes the first line that is not empty or a comment after the blank line, as the
    ///  code means to.
    /// </remarks>
    public static IReadOnlyList<EditorMark> CommitMessageMarks(IReadOnlyList<string> lines, char commentChar)
    {
        int summary = -1;
        bool seenDividingSpace = false;
        int descriptionStart = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            bool blank = string.IsNullOrWhiteSpace(lines[i]);
            if (summary == -1)
            {
                if (!blank && !IsComment(lines[i], commentChar))
                {
                    summary = i;
                }
            }
            else if (!seenDividingSpace && blank)
            {
                seenDividingSpace = true;
            }
            else if (!blank && !IsComment(lines[i], commentChar))
            {
                descriptionStart = i;
                break;
            }
        }

        List<EditorMark> marks = [];
        if (summary >= 0 && lines[summary].Length > MaxSummaryLength)
        {
            marks.Add(new EditorMark(summary, MaxSummaryLength, lines[summary].Length - MaxSummaryLength,
                SummaryTooLongText));
        }

        if (descriptionStart < 0)
        {
            return marks;
        }

        if (!seenDividingSpace)
        {
            marks.Add(new EditorMark(descriptionStart, 0, lines[descriptionStart].Length, SpacerNeededText));
        }

        // As upstream, every line from the description's first on, comments too.
        for (int i = descriptionStart; i < lines.Count; i++)
        {
            if (lines[i].Length > MaxDescriptionLength)
            {
                marks.Add(new EditorMark(i, MaxDescriptionLength, lines[i].Length - MaxDescriptionLength,
                    LineTooLongText));
            }
        }

        return marks;
    }

    // Upstream's IsComment: the first character that is not white space is the comment character.
    private static bool IsComment(string line, char commentChar)
        => line.FirstOrDefault(c => !char.IsWhiteSpace(c)) == commentChar;

    // Upstream's TryHighlightInteractiveRebaseCommand: the command (short or long form, with fixup's options) and the commit
    // id of at least four hex characters, followed by white space, are in the command's color.
    private static IReadOnlyList<EditorToken> TodoCommand(string line)
    {
        if (line.Length < 1 || !_commands.TryGetValue(line[0], out (string LongForm, EditorTokenKind Kind, string[] Options) command))
        {
            return [];
        }

        State state = State.Command;
        int index = 1;
        int idStart = -1;
        while (index < line.Length)
        {
            char c = line[index];
            switch (state)
            {
                case State.Command:
                    if ((index == 1 || index == command.LongForm.Length) && char.IsWhiteSpace(c))
                    {
                        state = State.SpacesAfterCommand;
                        int afterSpace = index + 1;
                        if (command.Options.FirstOrDefault(option => afterSpace + option.Length < line.Length
                                                                     && string.CompareOrdinal(line, afterSpace, option, 0,
                                                                         option.Length) == 0) is { } option)
                        {
                            index += option.Length + 1;
                            continue;
                        }
                    }
                    else if (index >= command.LongForm.Length || c != command.LongForm[index])
                    {
                        return [];
                    }

                    break;
                case State.SpacesAfterCommand:
                    if (IsHex(c))
                    {
                        idStart = index;
                        state = State.Id;
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        return [];
                    }

                    break;
                case State.Id:
                    if (char.IsWhiteSpace(c))
                    {
                        int idLength = index - idStart;
                        return idLength < MinimumIdLength
                            ? []
                            : [new EditorToken(0, idStart, command.Kind), new EditorToken(idStart, idLength, command.Kind)];
                    }

                    if (!IsHex(c))
                    {
                        return [];
                    }

                    break;
            }

            index++;
        }

        return [];

        static bool IsHex(char c) => char.IsDigit(c) || c is >= 'a' and <= 'f';
    }
}
