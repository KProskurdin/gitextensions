namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream FormCommit's "Conventional Commits" items: a commit type put before the subject line (replacing the type it
///  already has), and footers appended to the message.
/// </summary>
public static class ConventionalCommits
{
    /// <summary>
    ///  Upstream's <c>_feat</c>: the type the hotkeys select.
    /// </summary>
    public const string Feat = "feat";

    /// <summary>
    ///  Upstream's documentation link.
    /// </summary>
    public const string DocumentationUrl = "https://www.conventionalcommits.org";

    /// <summary>
    ///  Upstream's footer that keeps the caret where it is.
    /// </summary>
    public const string SkipCi = "[skip ci]";

    /// <summary>
    ///  Upstream's <c>_headerCommitTypes</c>.
    /// </summary>
    public static IReadOnlyList<string> HeaderTypes { get; } =
        ["build", "chore", "ci", "docs", Feat, "fix", "perf", "refactor", "style", "test"];

    /// <summary>
    ///  Upstream's <c>_footerKeywords</c>; each is appended as "keyword: ".
    /// </summary>
    public static IReadOnlyList<string> FooterKeywords { get; } = ["BREAKING CHANGE", "Co-authored-by", "Reviewed-by"];

    /// <summary>
    ///  Upstream's <c>PrefixOrReplaceKeyword</c> applied to the message: the new message and where the caret goes.
    ///  <paramref name="caret"/> is the caret position in <paramref name="message"/>; with <paramref name="insertScope"/>
    ///  (the hotkey with Shift) "()" follows the type and the caret goes into it.
    /// </summary>
    public static (string Message, int Caret) ApplyType(string message, int caret, string keyword, bool insertScope)
    {
        (string title, int selectionStart) = PrefixOrReplaceKeyword(message, caret, keyword, insertScope);
        if (message.Length == 0)
        {
            return (title, selectionStart);
        }

        // Upstream replaces the first line only.
        int end = FirstLineEnd(message);
        return (title + message[end..], selectionStart);
    }

    /// <summary>
    ///  Upstream's footer items: <paramref name="text"/> on a new line after the last line (after an empty first line when the
    ///  message is empty). The caret goes to the end, or stays for <see cref="SkipCi"/>.
    /// </summary>
    public static (string Message, int Caret) AppendFooter(string message, int caret, string text)
    {
        string newMessage = $"{message}{Environment.NewLine}{text}";
        return (newMessage, text == SkipCi ? caret : newMessage.Length);
    }

    private static int FirstLineEnd(string message)
    {
        int end = message.IndexOfAny(['\r', '\n']);
        return end < 0 ? message.Length : end;
    }

    private static (string Title, int SelectionStart) PrefixOrReplaceKeyword(string message, int currentPosition,
        string keyword, bool insertScope)
    {
        string scope = insertScope ? "()" : "";
        int scopePosition = keyword.Length + 1;
        int titlePosition = keyword.Length + (scope.Length / 2) + 2;

        string currentTitle = string.IsNullOrWhiteSpace(message) ? string.Empty : message[..FirstLineEnd(message)];

        // Replacing current keyword
        foreach (string key in HeaderTypes)
        {
            if (!currentTitle.StartsWith(key, StringComparison.Ordinal))
            {
                continue;
            }

            if (currentTitle.Length == key.Length)
            {
                return ($"{keyword}{scope}: ", insertScope ? scopePosition : titlePosition);
            }

            char nextChar = currentTitle[key.Length];
            if (!insertScope)
            {
                if (nextChar is ':' or '(' or '!')
                {
                    return ReplaceKeyword(key, _ => titlePosition);
                }
            }
            else
            {
                if (nextChar is ':' or '!')
                {
                    return ($"{keyword}(){currentTitle[key.Length..]}", scopePosition);
                }

                if (nextChar == '(')
                {
                    return ReplaceKeyword(key, newTitle => 2 + Math.Max(newTitle.IndexOf(':'), newTitle.IndexOf('(')));
                }
            }
        }

        // Append current keyword
        return ($"{keyword}{scope}: {currentTitle}", insertScope ? scopePosition : titlePosition + currentPosition);

        (string Title, int SelectionStart) ReplaceKeyword(string key, Func<string, int> maxPosition)
        {
            string newTitle = $"{keyword}{currentTitle[key.Length..]}";
            int newMessageLength = message.Length + newTitle.Length - currentTitle.Length;
            return (newTitle,
                Math.Min(newMessageLength, Math.Max(maxPosition(newTitle), currentPosition + keyword.Length - key.Length)));
        }
    }
}
