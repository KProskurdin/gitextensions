using System.Text;
using GitCommands;
using GitCommands.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  How a diff is shown, as upstream's <c>FileViewer</c> toolbar sets it: which whitespace changes to ignore, how many lines
///  of context to show around each change, or the entire file; upstream's histogram algorithm setting; and two choices
///  that change only the display, nonprinting characters and syntax highlighting.
/// </summary>
public sealed record DiffOptions(
    IgnoreWhitespaceKind IgnoreWhitespace = IgnoreWhitespaceKind.None,
    int ContextLines = DiffOptions.DefaultContextLines,
    bool ShowEntireFile = false,
    bool Histogram = false,
    bool ShowNonPrintingChars = false,
    bool ShowSyntaxHighlighting = true,
    bool TreatAllFilesAsText = false,
    Encoding? Encoding = null)
{
    /// <summary>
    ///  Upstream FileViewer's <c>Encoding</c>: the viewer's choice, else the repository's files encoding
    ///  (<c>Module.FilesEncoding</c>).
    /// </summary>
    public Encoding EncodingFor(GitModule module) => Encoding ?? module.FilesEncoding;

    /// <summary>
    ///  Upstream's default number of context lines (git's default too).
    /// </summary>
    public const int DefaultContextLines = 3;

    // Upstream's "entire file": git's context made larger than any file.
    private const int EntireFileContext = 9000;

    // Git's option that treats every file as text (diff and grep).
    private const string TextArgument = "--text";

    // Upstream FileViewer.GetExtraGrepArguments' context for the entire file.
    private const int EntireFileGrepContext = 100_000;

    /// <summary>
    ///  The git-grep arguments of a file's grep view, as upstream's <c>FileViewer.GetExtraGrepArguments</c> builds them: no
    ///  file names, and the lines around each match the context says (the whole file for the entire file).
    /// </summary>
    public IReadOnlyList<string> GrepArguments
        => TreatAllFilesAsText
            ? ["-h", $"--context={(ShowEntireFile ? EntireFileGrepContext : ContextLines)}", TextArgument]
            : ["-h", $"--context={(ShowEntireFile ? EntireFileGrepContext : ContextLines)}"];

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

            // Upstream's "Use histogram diff algorithm" (usehistogramdiffalgorithm), which GitModule adds to its diffs.
            if (Histogram)
            {
                arguments.Add("--histogram");
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

            // Upstream's "Treat all files as text": git shows a binary file's changes as text.
            if (TreatAllFilesAsText)
            {
                arguments.Add(TextArgument);
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
