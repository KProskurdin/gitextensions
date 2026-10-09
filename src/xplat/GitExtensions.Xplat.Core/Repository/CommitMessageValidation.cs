using System.Text.RegularExpressions;
using GitUI.CommandsDialogs.CommitDialog;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's commit message settings (<c>FormCommitTemplateSettings</c>, "Commit validation"), under upstream's keys.
/// </summary>
public sealed record CommitValidationOptions(
    int MaxFirstLineLength = 0,
    int MaxLineLength = 0,
    bool SecondLineMustBeEmpty = false,
    bool IndentAfterFirstLine = true,
    bool AutoWrap = true,
    string RegEx = "");

/// <summary>
///  Upstream FormCommit's checks of a commit message before it commits (<c>IsCommitMessageValid</c>), and its formatting
///  while the message is typed (<c>FormatAllText</c>): an empty second line and word-wrapped body lines.
/// </summary>
public static class CommitMessageValidation
{
    public const string Caption = "Commit validation";

    public const string FirstLineTooLong =
        "First line of commit message contains too many characters.\nDo you want to continue?";

    public const string SecondLineNotEmpty = "Second line of commit message is not empty.\nDo you want to continue?";

    public const string RegExNotMatched = "Commit message does not match RegEx.\nDo you want to continue?";

    // Upstream's message prefixes of a fixup, squash and amend commit (CommitKind.GetPrefix).
    private const string FixupPrefix = "fixup!";
    private const string SquashPrefix = "squash!";
    private const string AmendPrefix = "amend!";

    // Upstream's Delimiters.NewLines.
    private static readonly string[] _newLines = ["\r\n", "\n", "\r"];

    /// <summary>
    ///  Upstream's question for a line over the limit.
    /// </summary>
    public static string LineTooLong(string line)
        => $"The following line of commit message contains too many characters:\n\n{line}\n\nDo you want to continue?";

    /// <summary>
    ///  The questions upstream asks, in its order; the commit goes ahead only when each is answered yes. A broken regular
    ///  expression is not checked, as upstream.
    /// </summary>
    public static IEnumerable<string> Questions(string message, CommitValidationOptions options)
    {
        if (options.MaxFirstLineLength > 0)
        {
            string[] nonEmpty = message.Split(_newLines, StringSplitOptions.RemoveEmptyEntries);
            if (nonEmpty.Length > 0 && nonEmpty[0].Length > options.MaxFirstLineLength)
            {
                yield return FirstLineTooLong;
            }
        }

        if (options.MaxLineLength > 0)
        {
            foreach (string line in message.Split(_newLines, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > options.MaxLineLength)
                {
                    yield return LineTooLong(line);
                }
            }
        }

        if (options.SecondLineMustBeEmpty)
        {
            string[] lines = message.Split(_newLines, StringSplitOptions.None);
            if (lines.Length > 2 && lines[1].Length != 0)
            {
                yield return SecondLineNotEmpty;
            }
        }

        if (options.RegEx.Length > 0 && !message.StartsWith(FixupPrefix, StringComparison.Ordinal)
            && !message.StartsWith(SquashPrefix, StringComparison.Ordinal) && !MatchesOrInvalid(message, options.RegEx))
        {
            yield return RegExNotMatched;
        }
    }

    /// <summary>
    ///  The message as upstream formats it while it is typed: with "second line must be empty", text on the second line
    ///  moves down below an empty line (after " - " when lines after the first are indented); with auto-wrap, body lines over
    ///  the per-line limit are word-wrapped. The caret moves with the text. Upstream also colors the text over the limits;
    ///  the new shell's message box has one color.
    /// </summary>
    public static (string Message, int Caret) Format(string message, int caret, CommitValidationOptions options)
    {
        string newLine = message.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. message.Split(newLine)];
        if (options.SecondLineMustBeEmpty && lines.Count > 1 && lines[1].Length > 0)
        {
            string bullet = options.IndentAfterFirstLine ? " - " : "";
            int secondLineStart = lines[0].Length + newLine.Length;
            lines.Insert(1, "");
            lines[2] = bullet + lines[2];
            if (caret >= secondLineStart)
            {
                caret += newLine.Length + bullet.Length;
            }
        }

        if (options.AutoWrap && options.MaxLineLength > 0)
        {
            int start = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (i >= (options.SecondLineMustBeEmpty ? 2 : 1) && lines[i].Length > options.MaxLineLength)
                {
                    string wrapped = WordWrapper.WrapSingleLine(lines[i], options.MaxLineLength).Replace(Environment.NewLine, newLine);
                    if (caret > start + lines[i].Length)
                    {
                        caret += wrapped.Length - lines[i].Length;
                    }
                    else if (caret > start)
                    {
                        caret = Math.Min(caret, start + wrapped.Length);
                    }

                    lines[i] = wrapped;
                }

                start += lines[i].Length + newLine.Length;
            }
        }

        return (string.Join(newLine, lines), Math.Min(caret, string.Join(newLine, lines).Length));
    }

    /// <summary>
    ///  Upstream's "Word wrap (except subject line)" of the message box's menu: every line after the first is wrapped at
    ///  the per-line limit, or at 72 characters without one.
    /// </summary>
    public static string WrapBody(string message, int maxLineLength)
    {
        const int DefaultBodyLineLimit = 72;
        int limit = maxLineLength > 0 ? maxLineLength : DefaultBodyLineLimit;
        string newLine = message.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string[] lines = message.Split(newLine);
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > limit)
            {
                lines[i] = WordWrapper.WrapSingleLine(lines[i], limit).Replace(Environment.NewLine, newLine);
            }
        }

        return string.Join(newLine, lines);
    }

    private static bool MatchesOrInvalid(string message, string pattern)
    {
        try
        {
            return Regex.IsMatch(TextToValidate(message), pattern);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // Upstream's GetTextToValidate: an amend! commit is checked without its first two lines.
    private static string TextToValidate(string text)
    {
        if (!text.StartsWith(AmendPrefix, StringComparison.Ordinal) || text.IndexOfAny(['\r', '\n']) < 0)
        {
            return text;
        }

        string[] lines = text.Split(_newLines, StringSplitOptions.None);
        return lines.Length > 2 && lines[1].Length == 0 ? string.Join(Environment.NewLine, lines.AsSpan(2)) : text;
    }
}
