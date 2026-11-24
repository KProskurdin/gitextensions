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

    // Upstream's TranslatedStrings.ResetChangesCaption (GitUI).
    private const string ResetChangesCaption = "Reset changes";

    // Upstream's FilterByRevisionArg.
    private const string FilterByRevisionArgument = "--filter-by-revision";

    private readonly CommandLineRequest? _commandLineVerb;
    private readonly string _commandLineDirectory;

    /// <summary>
    ///  The repository a browse window opened by another one starts on (upstream's <c>StartBrowseDialog</c>).
    /// </summary>
    internal string? StartRepository { get; init; }

    /// <summary>
    ///  The filters and commits such a window applies once it has opened <see cref="StartRepository"/>.
    /// </summary>
    internal BrowseFilters? StartFilters { get; init; }

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
            or "branch" or "checkout" or "checkoutbranch" or "checkoutrevision" or "cherry" or "commit"
            or CommandLine.CompareVerb or "init" or "merge"
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

    // Upstream's StartInitializeDialog (the Start menu, the init verb and the plugins): FormInit on the folder given, else
    // on upstream's default clone destination; the new repository, which joins the recent ones as upstream's, or null.
    private async Task<string?> InitAsync(Window owner, string? folder)
    {
        InitRepositoryWindow window = new(folder ?? "", await AppServices.RecentRepositories.LoadAsync());
        if (!await window.ShowDialog<bool>(owner) || window.ViewModel.CreatedRepository is not { } created)
        {
            return null;
        }

        await _recent.AddAsync(created);
        return created;
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
            // Upstream's RunBlameCommand: the line after the file, when it is a number, is selected.
            case "blame":
                new BlameWindow(path, "HEAD", RepositoryFile(path, request.Arguments[0], currentDirectory),
                    initialLine: int.TryParse(request.Argument(1), out int line) ? line : null).Show(this);
                return true;

            // Upstream's RunFileHistoryCommand: a revision must be a commit's id, and only --filter-by-revision may follow.
            case "blamehistory":
            case "filehistory":
                if ((request.Argument(1) is { } revision && !ObjectId.TryParse(revision, out _))
                    || (request.Argument(2) is { } filter && filter != FilterByRevisionArgument))
                {
                    return false;
                }

                NewFileHistoryWindow(path, RepositoryFile(path, request.Arguments[0], currentDirectory), request.Argument(1),
                    showBlame: request.Verb == "blamehistory",
                    filterByRevision: request.Argument(2) is not null).Show(this);
                return true;
            case "branch":
                return await new CreateBranchWindow(path, _actions, _preferences).ShowDialog<bool>(this);
            case "checkout":
            case "checkoutbranch":
                return await StartCheckoutBranchAsync(path);
            case "checkoutrevision":
                return await ShowCheckoutRevisionAsync(path, revision: null);

            // Upstream's FormCherryPick on no commit yet, which its commit picker chooses.
            case "cherry":
                return await new CherryPickWindow(path, revision: null, _preferences,
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
                        [.. request.Arguments.Select(file => Path.GetFullPath(file, currentDirectory))]);
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

            // Upstream's RunCompareCommand: FormDiff on the two commits, an error on the console when they are not.
            case CommandLine.CompareVerb:
                return await CompareFromCommandLineAsync(request, path);

            // Upstream's StartCompareRevisionsDialog, whose FormLog is the log window here.
            case "viewdiff":
                await new LogWindow(path, _preferences).ShowDialog(this);
                return true;
            default:
                return false;
        }
    }

    // Upstream's RunCompareCommand and StartCompareRevisionsDialog: the two commits in the diff window, modal.
    private async Task<bool> CompareFromCommandLineAsync(CommandLineRequest request, string path)
    {
        GitModule module = GitModules.Open(path);
        (string? error, CompareRevisions? revisions) =
            await Task.Run(() => CommandLine.Compare(request, module.RevParse));
        if (revisions is null)
        {
            await Console.Error.WriteLineAsync(error);
            return false;
        }

        await new RevisionDiffWindow(path, revisions.First, revisions.Second, revisions.FirstDisplay,
            revisions.SecondDisplay, _preferences)
        {
            MenuServices = FileListMenuServices,
            Submodules = owner => DiffFileSubmodules(() => owner),
        }.ShowDialog(this);
        return true;
    }

    /// <summary>
    ///  Upstream's <c>BrowseArguments</c> of the <c>browse</c> and <c>openrepo</c> verbs, on the open repository:
    ///  <c>-filter=</c> in the "Filter:" box, <c>--pathFilter=</c> as the grid's path filter, and the first commit of
    ///  <c>-commit=</c> selected (upstream also keeps the second, for a diff).
    /// </summary>
    public Task ApplyBrowseArgumentsAsync(CommandLineRequest request)
    {
        string[] commits = CommandLine.Parameter(request, "-commit").Split(',');
        return ApplyBrowseFiltersAsync(CommandLine.Parameter(request, "-filter"),
            CommandLine.Parameter(request, "--pathFilter"), commits[0], commits.Length > 1 ? commits[1] : null,
            fileHistoryMode: request.Arguments.Any(argument => argument.StartsWith("--pathFilter", StringComparison.Ordinal)));
    }

    // Upstream's BrowseArguments: the "Filter:" box's text, the grid's path filter, the commit to select with the first
    // commit of a diff (selected first, as upstream's FirstId), and the file history mode, in which the File tree tab shows
    // blame and the left panel is collapsed while the window is open (upstream restores it when the window closes).
    private async Task ApplyBrowseFiltersAsync(string revisionFilter, string pathFilter, string? commit,
        string? firstCommit = null, bool fileHistoryMode = false)
    {
        if (fileHistoryMode)
        {
            _fileTreeBlame = true;
            if (MainSplitGrid.ColumnDefinitions[0].Width.Value > 0)
            {
                ToggleLeftPanel();
            }
        }

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

        if (!string.IsNullOrEmpty(commit) && !string.IsNullOrEmpty(firstCommit))
        {
            await GoToRevisionAsync(firstCommit, showNoRevisionMessage: true);
            await GoToRevisionAsync(commit, showNoRevisionMessage: true, toggleSelection: true);
        }
        else if (!string.IsNullOrEmpty(commit))
        {
            await GoToRevisionAsync(commit, showNoRevisionMessage: true);
        }
    }

    // Upstream's Commit: with --quiet, nothing to commit is no window and no failure; --message replaces the message.
    private bool Commit(IReadOnlyDictionary<string, string?> options)
    {
        if (options.ContainsKey("quiet") && _repository.Changes.Count == 0)
        {
            return true;
        }

        if (OpenCommitWindow() is not { } window)
        {
            return false;
        }

        if (options.GetValueOrDefault("message") is { } message)
        {
            window.ViewModel.Message = message;
        }

        return true;
    }

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

    // Upstream's StartResetChangesDialog(names): the changed files given or under the folders given ("." is all of
    // them), the question with whether they are tracked or new, then upstream's ResetChanges, which deletes the new ones
    // too when asked; its output, if any, in upstream's message.
    private async Task<bool> ResetFilesAsync(string path, IReadOnlyList<string> fullPaths)
    {
        GitModule module = GitModules.Open(path);
        string workingDir = module.WorkingDir;
        HashSet<string> files = [.. fullPaths.Select(name => Path.GetRelativePath(workingDir, name).ToPosixPath() ?? name)];
        HashSet<string> folders = [.. files.Where(name => Directory.Exists(Path.Join(workingDir, name)))];
        bool all = folders.Contains(".");
        IReadOnlyList<GitItemStatus> selected = await Task.Run(() =>
            (IReadOnlyList<GitItemStatus>)[.. module.GetAllChangedFilesWithSubmodulesStatus(cancellationToken: default)
                .Where(item => all || files.Contains(item.Name)
                               || folders.Any(folder => item.Path.Value.StartsWith(folder, StringComparison.Ordinal)))]);
        ResetChangesAction action = await ResetChangesWindow.ShowResetDialogAsync(this,
            hasExistingFiles: selected.Any(item => item.IsTracked), hasNewFiles: selected.Any(item => item.IsNew));
        if (action == ResetChangesAction.Cancel || selected.Count == 0)
        {
            return false;
        }

        string output = await Task.Run(() =>
        {
            module.ResetChanges(resetId: default, selected, resetAndDelete: action == ResetChangesAction.ResetAndDelete,
                new FullPathResolver(() => workingDir), out System.Text.StringBuilder text);
            return text.ToString();
        });
        if (output.Length > 0)
        {
            await new MessageBoxWindow(output, GitExtensions.Xplat.Ui.UpstreamTranslation.ByText(ResetChangesCaption),
                System.Windows.Forms.MessageBoxButtons.OK).ShowDialog(this);
        }

        RefreshRepository();
        return true;
    }

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

/// <summary>
///  Upstream's <c>BrowseArguments</c>: the "Filter:" box's text, the grid's path filter, the commit to select with the first
///  commit of a diff, and the file history mode.
/// </summary>
internal sealed record BrowseFilters(string RevisionFilter, string PathFilter, string? Commit, string? FirstCommit,
    bool FileHistoryMode);
