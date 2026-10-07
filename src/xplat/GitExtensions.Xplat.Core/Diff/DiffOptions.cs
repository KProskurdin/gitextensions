using GitCommands.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  How a diff is shown, as upstream's <c>FileViewer</c> toolbar sets it: which whitespace changes to ignore, how many lines
///  of context to show around each change, or the entire file.
/// </summary>
public sealed record DiffOptions(
    IgnoreWhitespaceKind IgnoreWhitespace = IgnoreWhitespaceKind.None,
    int ContextLines = DiffOptions.DefaultContextLines,
    bool ShowEntireFile = false)
{
    /// <summary>
    ///  Upstream's default number of context lines (git's default too).
    /// </summary>
    public const int DefaultContextLines = 3;

    // Upstream's "entire file": git's context made larger than any file.
    private const int EntireFileContext = 9000;

    /// <summary>
    ///  The git diff arguments, as upstream's <c>FileViewer.GetExtraDiffArguments</c> builds them.
    /// </summary>
    public IReadOnlyList<string> Arguments
    {
        get
        {
            List<string> arguments = [];
            switch (IgnoreWhitespace)
            {
                case IgnoreWhitespaceKind.AllSpace:
                    arguments.Add("--ignore-all-space");
                    break;
                case IgnoreWhitespaceKind.Change:
                    arguments.Add("--ignore-space-change");
                    break;
                case IgnoreWhitespaceKind.Eol:
                    arguments.Add("--ignore-space-at-eol");
                    break;
            }

            if (ShowEntireFile)
            {
                arguments.Add($"--inter-hunk-context={EntireFileContext}");
                arguments.Add($"--unified={EntireFileContext}");
            }
            else
            {
                arguments.Add($"--unified={ContextLines}");
            }

            return arguments;
        }
    }

    /// <summary>
    ///  Upstream's whitespace buttons: choosing the kind already on turns it off.
    /// </summary>
    public DiffOptions ToggleIgnoreWhitespace(IgnoreWhitespaceKind kind)
        => this with { IgnoreWhitespace = IgnoreWhitespace == kind ? IgnoreWhitespaceKind.None : kind };

    public DiffOptions WithMoreContext() => this with { ContextLines = ContextLines + 1 };

    /// <summary>
    ///  One line less, down to none, as upstream.
    /// </summary>
    public DiffOptions WithLessContext() => this with { ContextLines = Math.Max(0, ContextLines - 1) };

    public DiffOptions ToggleEntireFile() => this with { ShowEntireFile = !ShowEntireFile };
}
