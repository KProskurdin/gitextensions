using System.Globalization;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's command line verbs that run the browse window's own flows (<c>GitUICommands.RunCommandBasedOnArgument</c>
///  and the <c>Start...Dialog</c> methods it calls): the window hosts them unseen, so only the verb's windows show, as
///  upstream opens its forms without the browse window, with the same scripts, plugin events, PuTTY keys and process window
///  as from the menus. The window closes once the verb's work is done (<see cref="CommandLineExitCode"/>).
/// </summary>
public partial class MainWindow
{
    // How often the host looks whether the verb's work is done; it must be seen done twice in a row, as a flow can be
    // between a closed dialog and the operation the dialog asked for.
    private static readonly TimeSpan _commandLineIdleCheck = TimeSpan.FromMilliseconds(250);

    // The init verb's question, as the Start menu's "Create new repository...".
    private const string InitCaption = "Create new repository";
    private const string InitConfirmText = "Create a git repository in {0}?";

    private readonly CommandLineRequest? _commandLineVerb;
    private readonly string _commandLineDirectory;

    /// <summary>
    ///  Upstream's exit code of the command line verb this window ran: 0, or <see cref="CommandLine.FailedExitCode"/> when
    ///  the command failed or was cancelled.
    /// </summary>
    public int CommandLineExitCode { get; private set; } = CommandLine.FailedExitCode;

    /// <summary>
    ///  True for the verbs this window runs (<see cref="CommandLineVerbs"/> opens the others' windows itself).
    /// </summary>
    public static bool RunsCommandLineVerb(string verb)
        => verb is "add" or "addfiles" or "apply" or "applypatch" or "blame" or "blamehistory" or "filehistory"
            or "branch" or "checkout" or "checkoutbranch" or "checkoutrevision" or "cherry" or "commit" or "init" or "merge"
            or "mergeconflicts" or "mergetool" or "pull" or "push" or "rebase" or "reset" or "revert" or "stash"
            or "synchronize" or "tag" or "viewdiff";

    // A dot in the middle of the screen, out of the taskbar and the window list: the verb's windows need a shown owner, and
    // centred on it they are centred on the screen, as upstream's forms without an owner.
    private void HideAsCommandLineHost()
    {
        ShowInTaskbar = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CanResize = false;
        Width = 1;
        Height = 1;
        Opacity = 0;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private async Task RunCommandLineVerbAsync(CommandLineRequest request)
    {
        try
        {
            CommandLineExitCode = await RunVerbAsync(request) ? 0 : CommandLine.FailedExitCode;
            await WhenCommandLineVerbIdleAsync();
        }
        finally
        {
            Close();
        }
    }

    private async Task<bool> RunVerbAsync(CommandLineRequest request)
    {
        string currentDirectory = _commandLineDirectory;
        if (request.Verb == "init")
        {
            return await InitAsync(this, request.Argument(0) is { } folder
                ? Path.GetFullPath(folder, currentDirectory)
                : null) is not null;
        }

        if (CommandLine.WorkingDirectory(request, currentDirectory) is not { } workingDir)
        {
            return false;
        }

        await OpenRepositoryAsync(workingDir);
        return RepositoryPath is { } path && await RunRepositoryVerbAsync(request, path, currentDirectory);
    }

    // Upstream's StartInitializeDialog (the init verb): the folder given, after the question, else one to choose; the new
    // repository's folder, or null when none was made.
    private async Task<string?> InitAsync(Window owner, string? folder)
    {
        if (folder is null)
        {
            folder = await PickFolderAsync(InitCaption);
        }
        else if (!await new ConfirmWindow(string.Format(CultureInfo.CurrentCulture, InitConfirmText, folder), "Create",
                     InitCaption, cancelText: "Cancel").ShowDialog<bool>(owner))
        {
            return null;
        }

        return folder is not null && await _actions.InitAsync(folder) ? folder : null;
    }

    // A verb on the open repository at <path>, the files given from <currentDirectory>.
    private async Task<bool> RunRepositoryVerbAsync(CommandLineRequest request, string path, string currentDirectory)
    {
        IReadOnlyDictionary<string, string?> options = request.Options;
        switch (request.Verb)
        {
            // Upstream's StartAddFilesDialog: the files given, quoted, else the working directory.
            case "add":
            case "addfiles":
                await new AddFilesWindow(
                        request.Arguments.Count == 0 ? "." : string.Join(' ', request.Arguments.Select(file => file.Quote())),
                        arguments => RunGitWithOutputAsync(path, arguments))
                    .ShowDialog(this);
                return true;
            case "apply":
            case "applypatch":
            {
                ApplyPatchWindow apply = new(path, _preferences, arguments => RunGitWithOutputAsync(path, arguments),
                    ShowConflicts, RefreshRepository);
                if (request.Argument(0) is { } patch)
                {
                    apply.ViewModel.PatchFile = Path.GetFullPath(patch, currentDirectory);
                }

                await apply.ShowDialog(this);
                return true;
            }

            // Upstream's FormBlame on the file as it is checked out.
            case "blame":
                new BlameWindow(path, "HEAD", RepositoryFile(path, request.Arguments[0], currentDirectory)).Show(this);
                return true;
            case "blamehistory":
            case "filehistory":
                NewFileHistoryWindow(path, RepositoryFile(path, request.Arguments[0], currentDirectory), request.Argument(1),
                    showBlame: request.Verb == "blamehistory").Show(this);
                return true;
            case "branch":
                return await new CreateBranchWindow(path, _actions, _preferences).ShowDialog<bool>(this);
            case "checkout":
            case "checkoutbranch":
                return await StartCheckoutBranchAsync(path);
            case "checkoutrevision":
                return await ShowCheckoutRevisionAsync(path, revision: null);

            // Upstream's FormCherryPick starts on no commit; here on the checked out one, which the window's picker changes.
            case "cherry":
                return await new CherryPickWindow(path, "HEAD", _preferences,
                        (owner, commit, arguments) => CherryPickAsync(owner, path, commit, arguments))
                    .ShowDialog<bool>(this);
            case "commit":
                return Commit(options);
            case "mergeconflicts":
            case "mergetool":
                if (!options.ContainsKey("quiet") || GitModules.Open(path).InTheMiddleOfConflictedMerge())
                {
                    await NewConflictsWindow(path).ShowDialog(this);
                }

                return true;
            case "merge":
                return await ShowMergeAsync(path, options.GetValueOrDefault("branch"));
            case "pull":
                await PullAsync(options);
                return true;
            case "push":
                await PushAsync(options);
                return true;
            case "rebase":
                return await ShowRebaseAsync(path, options.GetValueOrDefault("branch"));

            // Upstream's StartResetChangesDialog: the files given, else all changes.
            case "reset":
            case "revert":
                return request.Arguments.Count == 0
                    ? await ResetChangesAsync(path)
                    : await ResetFilesAsync(path,
                        [.. request.Arguments.Select(file => RepositoryFile(path, file, currentDirectory))]);
            case "stash":
                await ShowStashWindowAsync(this);
                return true;

            // Upstream's RunSynchronizeCommand: commit, pull and push, each after the one before has finished.
            case "synchronize":
                bool committed = Commit(options);
                await WhenCommandLineVerbIdleAsync();
                await PullAsync(options);
                await WhenCommandLineVerbIdleAsync();
                await PushAsync(options);
                return committed;
            case "tag":
                return await ShowCreateTagAsync("HEAD");

            // Upstream's StartCompareRevisionsDialog, whose FormLog is the log window here.
            case "viewdiff":
                await new LogWindow(path, _preferences).ShowDialog(this);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///  Upstream's <c>BrowseArguments</c> of the <c>browse</c> and <c>openrepo</c> verbs, on the open repository:
    ///  <c>-filter=</c> in the "Filter:" box, <c>--pathFilter=</c> as the grid's path filter, and the first commit of
    ///  <c>-commit=</c> selected (upstream also keeps the second, for a diff).
    /// </summary>
    public Task ApplyBrowseArgumentsAsync(CommandLineRequest request)
        => ApplyBrowseFiltersAsync(CommandLine.Parameter(request, "-filter"), CommandLine.Parameter(request, "--pathFilter"),
            CommandLine.Parameter(request, "-commit").Split(',')[0]);

    // Upstream's BrowseArguments: the "Filter:" box's text, the grid's path filter and the commit to select (empty: none).
    private async Task ApplyBrowseFiltersAsync(string revisionFilter, string pathFilter, string? commit)
    {
        if (revisionFilter.Length > 0)
        {
            FilterToolStrip.SetRevisionFilter(revisionFilter);
        }

        if (pathFilter.Length > 0)
        {
            await ApplyRevisionFilterAsync(_commits.Filter with
            {
                PathFilter = (pathFilter.ToPosixPath() ?? pathFilter).QuoteNE(),
            });
        }

        if (!string.IsNullOrEmpty(commit))
        {
            await GoToRevisionAsync(commit, showNoRevisionMessage: true);
        }
    }

    // Upstream's Commit: with --quiet, nothing to commit is no window and no failure.
    private bool Commit(IReadOnlyDictionary<string, string?> options)
        => (options.ContainsKey("quiet") && _repository.Changes.Count == 0) || OpenCommitWindow() is not null;

    // Upstream's Pull: --merge, --rebase and --fetch become the stored default pull action (upstream stores them too),
    // --autostash turns auto stash on; --quiet pulls at once as the toolbar's Pull, else the pull dialog opens.
    private async Task PullAsync(IReadOnlyDictionary<string, string?> options)
    {
        if (options.ContainsKey("merge"))
        {
            _preferences.DefaultPullAction = GitPullAction.Merge;
        }

        if (options.ContainsKey("rebase"))
        {
            _preferences.DefaultPullAction = GitPullAction.Rebase;
        }

        if (options.ContainsKey("fetch"))
        {
            _preferences.DefaultPullAction = GitPullAction.Fetch;
        }

        if (options.ContainsKey("autostash"))
        {
            _preferences.AutoStash = true;
        }

        if (options.ContainsKey("quiet"))
        {
            Pull();
        }
        else
        {
            await ShowPullDialogAsync(this);
        }
    }

    // Upstream's Push: --quiet pushes at once to the tracked remote, as the toolbar's Push.
    private async Task PushAsync(IReadOnlyDictionary<string, string?> options)
    {
        if (options.ContainsKey("quiet"))
        {
            Push();
        }
        else
        {
            await ShowPushDialogAsync();
        }
    }

    // Upstream's FormResetChanges for the files given: their changes go back to HEAD.
    private async Task<bool> ResetFilesAsync(string path, IReadOnlyList<string> files)
        => await ResetChangesWindow.ShowResetDialogAsync(this, hasExistingFiles: true, hasNewFiles: false)
               != ResetChangesAction.Cancel
           && await _actions.ResetFilesToAsync(path, "HEAD", files);

    // Upstream's NormalizeFileName: a file given from anywhere, as git names it in the repository.
    private static string RepositoryFile(string repository, string file, string currentDirectory)
    {
        string relative = Path.GetRelativePath(repository, Path.GetFullPath(file, currentDirectory));
        return relative.ToPosixPath() ?? relative;
    }

    // The verb's work is done once nothing runs and no window it opened is open.
    private async Task WhenCommandLineVerbIdleAsync()
    {
        int idle = 0;
        while (idle < 2)
        {
            await Task.Delay(_commandLineIdleCheck);
            idle = OwnedWindows.Count == 0 && !IsBusy ? idle + 1 : 0;
        }
    }
}
