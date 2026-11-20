using GitCommands;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A command line with one of upstream's verbs (<c>GitUICommands.RunCommand</c>): the verb, the arguments after it, and
///  upstream's <c>--name value</c> options among them.
/// </summary>
/// <param name="Verb">The first argument, as given.</param>
/// <param name="Arguments">Every argument after the verb.</param>
/// <param name="Options">Upstream's <c>InitializeArguments</c>: each <c>--name</c> with the argument after it, or null.</param>
public sealed record CommandLineRequest(string Verb, IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string?> Options)
{
    /// <summary>
    ///  The argument at <paramref name="index"/> after the verb, or null when there are fewer.
    /// </summary>
    public string? Argument(int index) => index < Arguments.Count ? Arguments[index] : null;
}

/// <summary>
///  Upstream's command line, the part without windows: <c>GitUICommands.RunCommand</c> (the verbs and their checks),
///  <c>InitializeArguments</c>, <c>Program.GetWorkingDir</c> and <c>FormCommandlineHelp</c>'s text. The app opens each
///  verb's window (<c>CommandLineVerbs</c>).
/// </summary>
public static class CommandLine
{
    /// <summary>
    ///  Upstream's exit code when a command fails or is cancelled (<c>Program.RunApplication</c>).
    /// </summary>
    public const int FailedExitCode = -1;

    /// <summary>
    ///  Upstream's <c>FormCommandlineHelp</c> title.
    /// </summary>
    public const string UsageCaption = "Commandline usage";

    /// <summary>
    ///  Upstream's <c>FormCommandlineHelp</c> text: its label, then its list of commands.
    /// </summary>
    public const string Usage = """
        Supported commandline arguments for
        gitex.cmd / gitex (located in the same folder as GitExtensions.exe):

        [path]
        browse [path] [-filter=] [--pathFilter=<filepath>] [-commit=<selectedSha>[,<firstSha>]]
        about
        add [filename]
        addfiles [filename]
        apply [filename]
        applypatch [filename]
        blame filename
        branch
        checkout
        checkoutbranch
        checkoutrevision
        cherry
        cleanup
        clone [path]
        commit [--quiet] [--message commitmessage]
        difftool filename
        filehistory filename
        fileeditor filename
        formatpatch
        gitignore
        help (shows this dialog)
        init [path]
        merge [--branch name]
        mergeconflicts [--quiet]
        mergetool [--quiet]
        openrepo [path] [-filter=]
        pull [--rebase] [--merge] [--fetch] [--quiet] [--remotebranch name]
        push [--quiet]
        rebase [--branch name]
        remotes
        reset
        revert filename
        searchfile
        settings
        stash
        synchronize [--rebase] [--merge] [--fetch] [--quiet]
        tag
        viewdiff
        viewpatch [filename]
        """;

    private const string OptionPrefix = "--";

    // Upstream's checks before a verb runs: these need a file after them.
    private static readonly Dictionary<string, (string Text, string Caption)> _needsFile = new(StringComparer.Ordinal)
    {
        ["blame"] = ("Cannot open blame, there is no file selected.", "Blame"),
        ["difftool"] = ("Cannot open difftool, there is no file selected.", "Difftool"),
        ["blamehistory"] = ("Cannot open blame / file history, there is no file selected.", "Blame / file history"),
        ["filehistory"] = ("Cannot open blame / file history, there is no file selected.", "Blame / file history"),
        ["fileeditor"] = ("Cannot open file editor, there is no file selected.", "File editor"),
        ["revert"] = ("Cannot open revert, there is no file selected.", "Revert"),
    };

    /// <summary>
    ///  Every verb of upstream's <c>RunCommandBasedOnArgument</c>; anything else is a path, a URL or a request for help.
    /// </summary>
    public static IReadOnlySet<string> Verbs { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "about", "add", "addfiles", "apply", "applypatch", "blame", "blamehistory", "branch", "browse", "checkout",
        "checkoutbranch", "checkoutrevision", "cherry", "cleanup", "clone", "commit", "difftool", "filehistory",
        "fileeditor", "formatpatch", "gitignore", "init", "merge", "mergeconflicts", "mergetool", "openrepo", "pull",
        "push", "rebase", "remotes", "revert", "reset", "searchfile", "settings", "stash", "synchronize", "tag",
        "viewdiff", "viewpatch", "uninstall",
    };

    /// <summary>
    ///  The request in <paramref name="args"/> (the program's arguments, without its own path), or null without arguments.
    /// </summary>
    public static CommandLineRequest? Parse(IReadOnlyList<string> args)
        => args.Count == 0 ? null : new CommandLineRequest(args[0], [.. args.Skip(1)], Options(args));

    /// <summary>
    ///  Upstream's message and caption when <paramref name="request"/>'s verb needs a file and has none; null otherwise.
    /// </summary>
    public static (string Text, string Caption)? MissingFile(CommandLineRequest request)
        => request.Arguments.Count == 0 && _needsFile.TryGetValue(request.Verb, out (string Text, string Caption) message)
            ? message
            : null;

    /// <summary>
    ///  Upstream's <c>Program.GetWorkingDir</c> for a verb: the repository of the first argument after the verb (a folder,
    ///  or the folder of a file), else the repository around <paramref name="currentDirectory"/>; null outside one.
    /// </summary>
    public static string? WorkingDirectory(CommandLineRequest request, string currentDirectory)
    {
        string? workingDir = null;

        // .NET once unescaped a quote at the end of an argument badly (upstream's issue 3489).
        if (request.Argument(0)?.TrimEnd('"') is { } argument && !string.IsNullOrWhiteSpace(argument))
        {
            string? folder = Directory.Exists(argument) ? argument : Path.GetDirectoryName(argument);
            workingDir = string.IsNullOrEmpty(folder) ? null : GitModule.TryFindGitWorkingDir(folder);
            if (Directory.Exists(workingDir))
            {
                workingDir = Path.GetFullPath(workingDir);
            }
        }

        return workingDir ?? GitModule.TryFindGitWorkingDir(currentDirectory);
    }

    /// <summary>
    ///  Upstream's <c>GetParameterOrEmptyStringAsDefault</c>: the value of the first <c>name=value</c> argument after the
    ///  verb, or empty.
    /// </summary>
    public static string Parameter(CommandLineRequest request, string name)
    {
        string prefix = name + "=";
        return request.Arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.Ordinal))?
            [prefix.Length..] ?? "";
    }

    // Upstream's InitializeArguments: "--name value", or "--name" alone when the next argument is another option or none.
    private static Dictionary<string, string?> Options(IReadOnlyList<string> args)
    {
        Dictionary<string, string?> options = new(StringComparer.Ordinal);
        for (int index = 1; index < args.Count; index++)
        {
            if (!args[index].StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string name = args[index].TrimStart('-');
            if (index + 1 < args.Count && !args[index + 1].StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                options[name] = args[++index];
            }
            else
            {
                options[name] = null;
            }
        }

        return options;
    }
}
