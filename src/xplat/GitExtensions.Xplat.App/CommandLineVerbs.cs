using System.Globalization;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>GitUICommands.RunCommandBasedOnArgument</c>, the windows: a verb's window opens on its own, without the
///  browse window, as upstream's forms do, and the app ends with upstream's exit code when it closes. The verbs, their
///  checks and the working directory are core's <see cref="CommandLine"/>. <c>fileeditor</c> and <c>clone</c> are started
///  by <c>Program</c> and <c>MainWindow</c>; the verbs not ported yet say so.
/// </summary>
public static class CommandLineVerbs
{
    /// <summary>
    ///  The message for a verb whose window is not ported yet.
    /// </summary>
    public const string NotPortedText = "The command \"{0}\" is not available in this version of Git Extensions yet.";

    /// <summary>
    ///  The message for a verb that needs a repository, started outside one. Upstream ends without a word
    ///  (<c>DoActionOnRepo</c>).
    /// </summary>
    public const string NotARepositoryText = "\"{0}\" is not in a git repository.";

    private const string Caption = "Git Extensions";
    private const string BrowseVerb = "browse";
    private const string OpenRepoVerb = "openrepo";

    /// <summary>
    ///  True when <paramref name="request"/> asks for the browse window, as a path alone or upstream's <c>browse</c> and
    ///  <c>openrepo</c> verbs; <paramref name="repository"/> is then the repository to open, or null for the dashboard.
    /// </summary>
    public static bool OpensBrowseWindow(CommandLineRequest request, string currentDirectory, out string? repository)
    {
        repository = null;
        switch (request.Verb)
        {
            case BrowseVerb:
                repository = CommandLine.WorkingDirectory(request, currentDirectory);
                return true;

            // Upstream's RunOpenRepoCommand: the argument is a file whose first line is the repository's path.
            case OpenRepoVerb:
                repository = request.Argument(0) is { } file && File.Exists(file)
                             && File.ReadAllText(file).Trim().Split('\n').FirstOrDefault()?.Trim() is { } path
                             && Directory.Exists(path)
                    ? path
                    : GitModule.TryFindGitWorkingDir(currentDirectory);
                return true;
        }

        // Upstream's default case: a folder, or a file's folder, given alone.
        if (request.Arguments.Count > 0 || CommandLine.Verbs.Contains(request.Verb))
        {
            return false;
        }

        string folder = File.Exists(request.Verb) ? Path.GetDirectoryName(request.Verb) ?? request.Verb : request.Verb;
        if (!Directory.Exists(folder))
        {
            return false;
        }

        repository = folder;
        return true;
    }

    /// <summary>
    ///  Opens <paramref name="request"/>'s window on its own and calls <paramref name="exit"/> with upstream's exit code once
    ///  it closes: 0, or <see cref="CommandLine.FailedExitCode"/> when the command failed or was cancelled.
    /// </summary>
    public static Window Open(CommandLineRequest request, string currentDirectory, GitDiscoveryResult? git,
        Action<int> exit)
    {
        if (CommandLine.MissingFile(request) is { } missing)
        {
            return Message(missing.Text, missing.Caption, CommandLine.FailedExitCode, exit);
        }

        // Upstream's FormCommandlineHelp, for "help" and anything it does not know.
        if (!CommandLine.Verbs.Contains(request.Verb))
        {
            return Closing(new CommandLineHelpWindow(), exit);
        }

        string? repository = CommandLine.WorkingDirectory(request, currentDirectory);
        switch (request.Verb)
        {
            case "about":
                return Closing(new AboutWindow(git?.Version?.ToString()), exit);

            // Upstream's exit code tells whether the settings were saved (OK, or Apply before Cancel). Without the browse
            // window no plugin is loaded, so their pages are missing.
            case "settings":
            {
                SettingsWindow settings = new(AppServices.Preferences, repository);
                return Closing(settings, exit, () => settings.ViewModel.Saved ? 0 : CommandLine.FailedExitCode);
            }

            case "viewpatch":
                return Closing(new ViewPatchWindow(repository ?? currentDirectory, request.Argument(0)), exit);

            // Upstream's FormInit: a new repository, which needs none.
            case "init":
                return Host(request, currentDirectory, git, exit);
        }

        if (!IsPorted(request.Verb))
        {
            return Message(string.Format(CultureInfo.CurrentCulture, NotPortedText, request.Verb), Caption,
                CommandLine.FailedExitCode, exit);
        }

        if (repository is null)
        {
            return Message(string.Format(CultureInfo.CurrentCulture, NotARepositoryText,
                request.Argument(0) ?? currentDirectory), Caption, CommandLine.FailedExitCode, exit);
        }

        return request.Verb switch
        {
            "cleanup" => Closing(new CleanupRepositoryWindow(repository, cleaned: null), exit),
            "formatpatch" => Closing(new FormatPatchWindow(repository, AppServices.Preferences), exit),
            "gitignore" => Closing(new GitIgnoreWindow(repository), exit),
            "remotes" => Closing(new RemotesWindow(repository, AppServices.Preferences), exit),
            "searchfile" => SearchFile(repository, exit),
            _ => Host(request, currentDirectory, git, exit),
        };
    }

    /// <summary>
    ///  True for the verbs that open no window: upstream's <c>difftool</c> with its file, and <c>uninstall</c>.
    /// </summary>
    public static bool OpensNoWindow(CommandLineRequest request)
        => request.Verb == "uninstall" || (request.Verb == "difftool" && request.Arguments.Count > 0);

    /// <summary>
    ///  Runs a verb that opens no window (<see cref="OpensNoWindow"/>) and returns upstream's exit code.
    /// </summary>
    public static async Task<int> RunWithoutWindowAsync(CommandLineRequest request, string currentDirectory,
        IGitConfigService config)
    {
        // Upstream's UninstallEditor, which its Windows installer runs: core.editor no longer names this app.
        if (request.Verb == "uninstall")
        {
            await GitEditorUninstall.RunAsync(config, AppSettings.GetInstallDir());
            return 0;
        }

        // Upstream's difftool verb: the configured diff tool on the file's changes in the working directory.
        string file = request.Arguments[0];
        if (CommandLine.WorkingDirectory(request, currentDirectory) is not { } repository)
        {
            await Console.Error.WriteLineAsync(string.Format(CultureInfo.CurrentCulture, NotARepositoryText, file));
            return CommandLine.FailedExitCode;
        }

        try
        {
            string relative = Path.GetRelativePath(repository, Path.GetFullPath(file, currentDirectory));
            await Task.Run(() => GitModules.Open(repository).OpenWithDifftool(relative.ToPosixPath() ?? relative));
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            return CommandLine.FailedExitCode;
        }
    }

    /// <summary>
    ///  True for the verbs this version runs.
    /// </summary>
    public static bool IsPorted(string verb)
        => verb is "about" or "settings" or "viewpatch" or "cleanup" or "formatpatch" or "gitignore" or "remotes"
            or "searchfile" or "difftool" or "uninstall" or BrowseVerb or OpenRepoVerb
           || MainWindow.RunsCommandLineVerb(verb);

    // The verbs that run the browse window's flows: the window, unseen, owns their windows.
    private static Window Host(CommandLineRequest request, string currentDirectory, GitDiscoveryResult? git,
        Action<int> exit)
    {
        MainWindow host = new(git, request, currentDirectory);
        return Closing(host, exit, () => host.CommandLineExitCode);
    }

    private static Window Closing(Window window, Action<int> exit, Func<int>? exitCode = null)
    {
        window.Closed += (_, _) => exit(exitCode?.Invoke() ?? 0);
        return window;
    }

    private static Window Message(string text, string caption, int exitCode, Action<int> exit)
        => Closing(new MessageBoxWindow(text, caption, MessageBoxButtons.OK), exit, () => exitCode);

    // Upstream's RunSearchFileCommand: HEAD's files, read once by the first search (off the UI thread); the file taken is
    // printed with the repository's path, which the Visual Studio extension reads, and no file taken is a failure.
    private static Window SearchFile(string repository, Action<int> exit)
    {
        Lazy<IReadOnlyList<(string Path, string? OldPath)>> files = new(() =>
            [.. GitCommitHistory.LoadTree(repository, "HEAD").Select(file => (file, (string?)null))]);
        SearchWindow search = new(text => FindFile.Matches(files.Value, text, repository));
        return Closing(search, exit, () =>
        {
            if (search.SelectedItem is not { } file)
            {
                return CommandLine.FailedExitCode;
            }

            Console.Out.WriteLine(Path.Combine(repository, file));
            Console.Out.Flush();
            return 0;
        });
    }
}
