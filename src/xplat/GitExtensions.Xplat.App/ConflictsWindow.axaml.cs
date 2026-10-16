using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormResolveConflicts</c> in the new shell (rules in <see cref="ConflictsDialog"/>): the unresolved files,
///  merged with the configured merge tool (run by the app with its <c>cmd</c>, or through git mergetool), resolved by
///  choosing a side, deleted or kept when one side deleted them, and opened or saved per side; Rescan and Reset; once the
///  last conflict is resolved it offers to commit and closes.
/// </summary>
public partial class ConflictsWindow : Window
{
    private const string Error = "Error";
    private const string Warning = "Warning";
    private const string HelpSubfolder = "modify_history";
    private const string HelpAnchor = "handle-merge-conflicts";

    private readonly string _repositoryPath;
    private readonly RepositoryViewModel _repository;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly Action<Window, string> _showFileHistory;
    private readonly Action<Window>? _showCommit;
    private readonly Func<string, Task<bool>>? _updateSubmodules;
    private readonly bool _offerCommit;
    private readonly GitModule _module;

    private readonly IFileManager _fileManager =
        new SystemFileManager(new SystemProcessLauncher(), HostPlatform.Current);

    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ConflictsDialog _dialog = new(inRebase: false);
    private MergeTool _tool = new(null, null, null, null);
    private bool _thereWereConflicts;
    private bool _applyToAll;
    private int _deletedLocallyUnsolved;
    private int _deletedRemotelyUnsolved;
    private int _selectedCount;
    private bool _othersSelected;
    private ConflictChoice _lastChoice;
    private Task _work = Task.CompletedTask;

    /// <param name="showFileHistory">Opens the file history window on a file, over this one.</param>
    /// <param name="showCommit">Opens the commit window, as upstream offers once every conflict is resolved.</param>
    /// <param name="updateSubmodules">Upstream's <c>UpdateSubmodules</c> once the conflicts are resolved.</param>
    /// <param name="offerCommit">Upstream's <c>offerCommit</c>: false when the commit window opened this one.</param>
    public ConflictsWindow(string repositoryPath, RepositoryViewModel repository, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, Action<Window, string> showFileHistory, Action<Window>? showCommit = null,
        Func<string, Task<bool>>? updateSubmodules = null, bool offerCommit = true)
    {
        _repositoryPath = repositoryPath;
        _repository = repository;
        _actions = actions;
        _preferences = preferences;
        _showFileHistory = showFileHistory;
        _showCommit = showCommit;
        _updateSubmodules = updateSubmodules;
        _offerCommit = offerCommit;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.ConflictsWindow");

        ConflictList.SelectionChanged += (_, _) => ShowSelection();
        ConflictList.DoubleTapped += (_, _) => Run(OpenMergeToolAsync);
        MergeButton.Click += (_, _) => Run(OpenMergeToolAsync);
        OpenMergeToolButton.Click += (_, _) => Run(OpenMergeToolAsync);
        OpenMergeToolMenuItem.Click += (_, _) => Run(OpenMergeToolAsync);
        CustomMergeToolMenuItem.Click += (_, _) => Run(RunGitMergeToolOnSelectedAsync);
        StartMergeToolButton.Click += (_, _) => Run(StartMergeToolAsync);
        RescanButton.Click += (_, _) => Run(InitializeAsync);
        ResetButton.Click += (_, _) => Run(ResetAsync);
        MarkAsSolvedMenuItem.Click += (_, _) => Run(MarkAsSolvedAsync);
        ChooseLocalMenuItem.Click += (_, _) => Run(() => ChooseAsync(ConflictsDialog.Local));
        ChooseRemoteMenuItem.Click += (_, _) => Run(() => ChooseAsync(ConflictsDialog.Remote));
        ChooseBaseMenuItem.Click += (_, _) => Run(() => ChooseAsync(ConflictsDialog.Base));
        OpenLocalWithMenuItem.Click += (_, _) => Run(() => OpenSideWithAsync(ConflictsDialog.Local));
        OpenRemoteWithMenuItem.Click += (_, _) => Run(() => OpenSideWithAsync(ConflictsDialog.Remote));
        OpenBaseWithMenuItem.Click += (_, _) => Run(() => OpenSideWithAsync(ConflictsDialog.Base));
        SaveLocalAsMenuItem.Click += (_, _) => Run(() => SaveSideAsync(ConflictsDialog.Local));
        SaveRemoteAsMenuItem.Click += (_, _) => Run(() => SaveSideAsync(ConflictsDialog.Remote));
        SaveBaseAsMenuItem.Click += (_, _) => Run(() => SaveSideAsync(ConflictsDialog.Base));
        OpenMenuItem.Click += (_, _) => Run(OpenFileAsync);
        OpenWithMenuItem.Click += (_, _) => Run(OpenFileAsync);
        ShowInFolderMenuItem.Click += (_, _) =>
        {
            if (Selected() is [{ } item])
            {
                _fileManager.OpenFolder(Path.GetDirectoryName(FullPath(item.Filename)) ?? _repositoryPath);
            }
        };
        FileHistoryMenuItem.Click += (_, _) =>
        {
            if (Selected() is [{ } item])
            {
                _showFileHistory(this, item.Filename);
            }
        };
        ConflictMenu.Opening += (_, e) => e.Cancel = !PrepareMenu();
        HelpLink.Click += (_, _) =>
            OsShellUtil.OpenUrlInDefaultBrowser(GitUI.UserManual.UserManual.UrlFor(HelpSubfolder, HelpAnchor));
        KeyDown += OnWindowKeyDown;
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  The unresolved files on show.
    /// </summary>
    public IReadOnlyList<ConflictData> Conflicts { get; private set; } = [];

    /// <summary>
    ///  Completes when the merge tool is read and the files are listed, for tests.
    /// </summary>
    public Task ConflictsLoaded => _loaded.Task;

    /// <summary>
    ///  The last action's work, for tests.
    /// </summary>
    public Task Work => _work;

    // Upstream's OnRuntimeLoad and FormResolveConflicts_Load: whether there were conflicts, the merge tool, the list.
    private async Task LoadAsync()
    {
        _thereWereConflicts = await Task.Run(() => _module.InTheMiddleOfConflictedMerge());
        _tool = await Task.Run(() => ConflictsDialog.ReadMergeTool(key => _module.GetEffectiveSetting(key),
            GitVersion.Current.SupportGuiMergeTool, OperatingSystem.IsWindows(),
            (string name, out string? fullPath) => PathUtil.TryFindFullPath(name, out fullPath),
            UpstreamTranslation.ByText));
        if (_tool.Error is { } error)
        {
            await ShowMessageAsync(error, _tool.Name is null ? Error : Warning);
        }

        await InitializeAsync();
        MergeButton.Focus();
        _loaded.TrySetResult();
    }

    // Upstream's Initialize: the files (keeping the selected row), the texts for a merge or a rebase, and once no conflict
    // is left the submodule update, the offer to commit and the close.
    private async Task InitializeAsync()
    {
        (bool inRebase, IReadOnlyList<ConflictData> conflicts) = await Task.Run(async () =>
            (_module.InTheMiddleOfRebase(), (IReadOnlyList<ConflictData>)await _module.GetConflictsAsync()));
        int oldIndex = Math.Max(0, ConflictList.SelectedIndex);
        _dialog = new ConflictsDialog(inRebase);
        Conflicts = conflicts;
        List<ConflictRow> rows = [.. conflicts.Select(conflict => new ConflictRow(conflict))];
        ConflictList.ItemsSource = rows;
        if (rows.Count > 0)
        {
            ConflictList.SelectedIndex = Math.Min(oldIndex, rows.Count - 1);
        }

        string openIn = $"{UpstreamTranslation.ByText(ConflictsDialog.OpenIn)} {_tool.Name}";
        OpenMergeToolMenuItem.Header = openIn;
        OpenMergeToolButton.Content = openIn;
        ChooseLocalMenuItem.Header = UpstreamTranslation.ByText(_dialog.ChooseLocalText);
        ToolTip.SetTip(ChooseLocalMenuItem, UpstreamTranslation.ByText(_dialog.ChooseLocalTooltip));
        ChooseRemoteMenuItem.Header = UpstreamTranslation.ByText(_dialog.ChooseRemoteText);
        ToolTip.SetTip(ChooseRemoteMenuItem, UpstreamTranslation.ByText(_dialog.ChooseRemoteTooltip));
        ToolTip.SetTip(ChooseBaseMenuItem, UpstreamTranslation.ByText(ConflictsDialog.ChooseBaseTooltip));
        LocalLabelText.Text = _dialog.LocalLabelText(UpstreamTranslation.ByText(ConflictsDialog.LocalLabel));
        ToolTip.SetTip(LocalLabelText, UpstreamTranslation.ByText(_dialog.LocalLabelTooltip));
        RemoteLabelText.Text = _dialog.RemoteLabelText(UpstreamTranslation.ByText(ConflictsDialog.RemoteLabel));
        ToolTip.SetTip(RemoteLabelText, UpstreamTranslation.ByText(_dialog.RemoteLabelTooltip));
        ShowSelection();
        await _repository.RefreshAsync(_repositoryPath);

        (bool conflicted, bool inPatch) = await Task.Run(() =>
            (_module.InTheMiddleOfConflictedMerge(), _module.InTheMiddleOfPatch()));
        if (!conflicted && _thereWereConflicts)
        {
            if (_updateSubmodules is not null)
            {
                await _updateSubmodules(_repositoryPath);
            }

            if (!inPatch && !inRebase && _offerCommit
                && await ConfirmWindow.AskAsync(this, _preferences, Confirmation.CommitAfterConflictsResolved,
                    UpstreamTranslation.ByText(ConflictsDialog.AllConflictsResolved), "Yes",
                    UpstreamTranslation.ByText(ConflictsDialog.AllConflictsResolvedCaption)))
            {
                _showCommit?.Invoke(Owner as Window ?? this);
            }

            Close();
        }
    }

    // Upstream's UpdateConflictedFilesMenu: one file shows its description and names; several disable the one-file items.
    private void ShowSelection()
    {
        BaseNameText.Text = LocalNameText.Text = RemoteNameText.Text = "";
        IReadOnlyList<ConflictData> selected = Selected();
        SetAvailableCommands(selected.Count == 1);
        if (selected is not [{ } item])
        {
            return;
        }

        if (_dialog.Description(item, UpstreamTranslation.ByText) is { } description)
        {
            ConflictDescriptionText.Text = description;
        }

        (string baseName, string localName, string remoteName) = ConflictsDialog.Names(item,
            ItemTypeOf(item.Filename) == ItemType.Submodule, UpstreamTranslation.ByText);
        BaseNameText.Text = baseName;
        LocalNameText.Text = localName;
        RemoteNameText.Text = remoteName;
    }

    // Upstream's SetAvailableCommands: the items that need one file, and the app's own run of the tool.
    private void SetAvailableCommands(bool enabled)
    {
        OpenMergeToolMenuItem.IsEnabled = enabled && _tool.HasExtras;
        OpenMergeToolButton.IsEnabled = enabled && _tool.HasExtras;
        CustomMergeToolMenuItem.IsEnabled = enabled;
        foreach (MenuItem item in new[]
                 {
                     OpenLocalWithMenuItem, OpenRemoteWithMenuItem, OpenBaseWithMenuItem, SaveLocalAsMenuItem,
                     SaveRemoteAsMenuItem, SaveBaseAsMenuItem, OpenMenuItem, OpenWithMenuItem, ShowInFolderMenuItem,
                     FileHistoryMenuItem,
                 })
        {
            item.IsEnabled = enabled;
        }
    }

    // Upstream's ConflictedFilesContextMenu_Opening: a side the file does not have cannot be opened or saved.
    private bool PrepareMenu()
    {
        IReadOnlyList<ConflictData> selected = Selected();
        if (selected.Count == 0)
        {
            return false;
        }

        SetAvailableCommands(selected.Count == 1);
        if (selected is [{ } item])
        {
            if (!ConflictsDialog.HasFile(item.Local))
            {
                SaveLocalAsMenuItem.IsEnabled = OpenLocalWithMenuItem.IsEnabled = false;
            }

            if (!ConflictsDialog.HasFile(item.Remote))
            {
                SaveRemoteAsMenuItem.IsEnabled = OpenRemoteWithMenuItem.IsEnabled = false;
            }

            if (!ConflictsDialog.HasFile(item.Base))
            {
                SaveBaseAsMenuItem.IsEnabled = OpenBaseWithMenuItem.IsEnabled = false;
            }
        }

        return true;
    }

    // Upstream's OpenMergeTool: the files deleted locally first, then those deleted remotely, then the rest, each through the
    // merge tool or the question for its case.
    private async Task OpenMergeToolAsync()
    {
        IReadOnlyList<ConflictData> selected = Selected();
        if (selected.Count == 0)
        {
            return;
        }

        (IReadOnlyList<ConflictData> deletedLocally, IReadOnlyList<ConflictData> deletedRemotely,
            IReadOnlyList<ConflictData> remaining) = Count(selected);
        try
        {
            _applyToAll = false;
            foreach (ConflictData item in deletedLocally)
            {
                Progress.Value++;
                await ResolveItemAsync(item);
            }

            _applyToAll = false;
            foreach (ConflictData item in deletedRemotely)
            {
                Progress.Value++;
                await ResolveItemAsync(item);
            }

            _applyToAll = false;
            foreach (ConflictData item in remaining)
            {
                Progress.Value++;
                await ResolveItemAsync(item);
            }
        }
        finally
        {
            _applyToAll = false;
            Progress.IsVisible = false;
            await InitializeAsync();
        }
    }

    // Upstream's GetConflicts: the counts its questions use, and the progress bar.
    private (IReadOnlyList<ConflictData>, IReadOnlyList<ConflictData>, IReadOnlyList<ConflictData>) Count(
        IReadOnlyList<ConflictData> selected)
    {
        (IReadOnlyList<ConflictData> deletedLocally, IReadOnlyList<ConflictData> deletedRemotely,
            IReadOnlyList<ConflictData> remaining) = ConflictsDialog.Classify(selected);
        _selectedCount = selected.Count;
        _deletedLocallyUnsolved = deletedLocally.Count;
        _deletedRemotelyUnsolved = deletedRemotely.Count;
        _othersSelected = remaining.Count > 0;
        Progress.Maximum = selected.Count;
        Progress.Value = 0;
        Progress.IsVisible = true;
        return (deletedLocally, deletedRemotely, remaining);
    }

    // Upstream's ResolveItemConflictAsync. A submodule's conflict needs upstream's FormMergeSubmodule, which is not ported.
    private Task ResolveItemAsync(ConflictData item)
        => ItemTypeOf(item.Filename) == ItemType.File ? ResolveFileAsync(item) : Task.CompletedTask;

    // Upstream's ResolveFilesConflictAsync: the three versions checked out to temporary files, the questions for a missing
    // side or a binary file, then the tool with upstream's command, and the file staged as its exit code and the merged
    // file's time say.
    private async Task ResolveFileAsync(ConflictData item)
    {
        (string? baseFile, string? localFile, string? remoteFile) =
            await Task.Run(() => KeepCurrentDirectory(() => _module.CheckoutConflictedFiles(item)));
        try
        {
            if (!await CheckForLocalRevisionAsync(item) || !await CheckForRemoteRevisionAsync(item))
            {
                return;
            }

            if (await Task.Run(() => FileHelper.IsBinaryFileName(_module, item.Local.Filename))
                && await AskAsync(string.Format(UpstreamTranslation.ByText(ConflictsDialog.FileIsBinary), _tool.Name),
                    Warning, MessageBoxButtons.YesNo, MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                await BinaryFileChooseAsync(item);
                return;
            }

            if (string.IsNullOrWhiteSpace(_tool.Command) || string.IsNullOrWhiteSpace(_tool.Path))
            {
                // Upstream: the tool is set but its arguments cannot be changed; git mergetool gives no exit status, so
                // nothing is staged here.
                await _actions.RunMergeToolAsync(_repositoryPath, item.Filename);
                return;
            }

            string arguments = _tool.Command;
            if (item.Base.Filename is null)
            {
                DialogResult result = await AskAsync(
                    string.Format(UpstreamTranslation.ByText(ConflictsDialog.NoBaseRevision), item.Filename),
                    UpstreamTranslation.ByText(ConflictsDialog.NoBaseFileMergeCaption), MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Cancel)
                {
                    return;
                }

                if (result == DialogResult.Yes)
                {
                    arguments = ConflictsDialog.TwoWayMergeArguments(_tool.Name ?? "", arguments);
                }
            }

            arguments = ConflictsDialog.MergeToolArguments(arguments, baseFile, localFile, remoteFile, item.Filename);
            string filePath = FullPath(item.Filename);
            DateTime before = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : DateTime.Now;
            ExecutionResult run;
            try
            {
                run = await new Executable(_tool.Path, _repositoryPath).ExecuteAsync(arguments,
                    throwOnErrorExit: false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await ShowMessageAsync(string.Format(UpstreamTranslation.ByText(ConflictsDialog.ErrorStartingMergetool),
                    _tool.Path), UpstreamTranslation.ByText(ConflictsDialog.NoBaseFileMergeCaption));
                return;
            }

            DateTime after = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : before;
            switch (ConflictsDialog.Outcome(run.ExitCode, after != before))
            {
                case MergeToolOutcome.Stage:
                    await StageFileAsync(item.Filename);
                    break;
                case MergeToolOutcome.Ask when await AskAsync(
                    UpstreamTranslation.ByText(ConflictsDialog.AskMergeConflictSolved),
                    UpstreamTranslation.ByText(ConflictsDialog.AskMergeConflictSolvedCaption),
                    MessageBoxButtons.YesNo) == DialogResult.Yes:
                    await StageFileAsync(item.Filename);
                    break;
            }
        }
        finally
        {
            foreach (string? file in new[] { baseFile, localFile, remoteFile })
            {
                if (file is not null && File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    // Upstream's CheckForLocalRevision: a file deleted locally is deleted, kept as modified remotely, or set to base.
    private async Task<bool> CheckForLocalRevisionAsync(ConflictData item)
    {
        if (ConflictsDialog.HasFile(item.Local))
        {
            return true;
        }

        string text = _dialog.DeletedSideText(item, deletedLocally: true, _deletedLocallyUnsolved, _selectedCount,
            _othersSelected || _deletedRemotelyUnsolved > 0, _applyToAll, UpstreamTranslation.ByText);
        (string local, string remote, string baseText) = _dialog.DeletedSideButtons(deletedLocally: true,
            UpstreamTranslation.ByText);
        ConflictChoice choice =
            await SolveAsync(text, ApplyToAllText(item, _deletedLocallyUnsolved), local, remote, baseText);
        switch (choice)
        {
            case ConflictChoice.KeepLocal:
                await RemoveFileAsync(item.Filename);
                break;
            case ConflictChoice.KeepRemote:
                await SelectSideAsync(item.Filename, ConflictsDialog.Remote);
                break;
            case ConflictChoice.KeepBase:
                await SelectSideAsync(item.Filename, ConflictsDialog.Base);
                break;
        }

        _deletedLocallyUnsolved--;
        return false;
    }

    // Upstream's CheckForRemoteRevision: a file deleted remotely is kept as modified locally, deleted, or set to base.
    private async Task<bool> CheckForRemoteRevisionAsync(ConflictData item)
    {
        if (ConflictsDialog.HasFile(item.Remote))
        {
            return true;
        }

        string text = _dialog.DeletedSideText(item, deletedLocally: false, _deletedRemotelyUnsolved, _selectedCount,
            _othersSelected || _deletedLocallyUnsolved > 0, _applyToAll, UpstreamTranslation.ByText);
        (string local, string remote, string baseText) = _dialog.DeletedSideButtons(deletedLocally: false,
            UpstreamTranslation.ByText);
        ConflictChoice choice =
            await SolveAsync(text, ApplyToAllText(item, _deletedRemotelyUnsolved), local, remote, baseText);
        switch (choice)
        {
            case ConflictChoice.KeepLocal:
                await SelectSideAsync(item.Filename, ConflictsDialog.Local);
                break;
            case ConflictChoice.KeepRemote:
                await RemoveFileAsync(item.Filename);
                break;
            case ConflictChoice.KeepBase:
                await SelectSideAsync(item.Filename, ConflictsDialog.Base);
                break;
        }

        _deletedRemotelyUnsolved--;
        return false;
    }

    // Upstream's CheckForBaseRevision: a file created on both sides keeps the local or remote one, or is deleted.
    private async Task<bool> CheckForBaseRevisionAsync(ConflictData item)
    {
        if (ConflictsDialog.HasFile(item.Base))
        {
            return true;
        }

        ConflictChoice choice = await SolveAsync(
            string.Format(UpstreamTranslation.ByText(ConflictsDialog.FileCreatedLocallyAndRemotelyLong), item.Filename,
                _dialog.LocalSide, _dialog.RemoteSide), "",
            $"{UpstreamTranslation.ByText(ConflictsDialog.ChooseLocalButtonText)} ({_dialog.LocalSide})",
            $"{UpstreamTranslation.ByText(ConflictsDialog.ChooseRemoteButtonText)} ({_dialog.RemoteSide})",
            UpstreamTranslation.ByText(ConflictsDialog.DeleteFileButtonText));
        switch (choice)
        {
            case ConflictChoice.KeepLocal:
                await SelectSideAsync(item.Filename, ConflictsDialog.Local);
                break;
            case ConflictChoice.KeepRemote:
                await SelectSideAsync(item.Filename, ConflictsDialog.Remote);
                break;
            case ConflictChoice.KeepBase:
                await RemoveFileAsync(item.Filename);
                break;
        }

        return false;
    }

    // Upstream's BinaryFilesChooseLocalBaseRemote.
    private async Task BinaryFileChooseAsync(ConflictData item)
    {
        ConflictChoice choice = await SolveAsync(
            string.Format(UpstreamTranslation.ByText(ConflictsDialog.FileBinaryChooseLocalBaseRemote),
                item.Local.Filename,
                _dialog.LocalSide, _dialog.RemoteSide), "",
            $"{UpstreamTranslation.ByText(ConflictsDialog.ChooseLocalButtonText)} ({_dialog.LocalSide})",
            $"{UpstreamTranslation.ByText(ConflictsDialog.ChooseRemoteButtonText)} ({_dialog.RemoteSide})",
            UpstreamTranslation.ByText(ConflictsDialog.KeepBaseButtonText));
        string? side = choice switch
        {
            ConflictChoice.KeepLocal => ConflictsDialog.Local,
            ConflictChoice.KeepRemote => ConflictsDialog.Remote,
            ConflictChoice.KeepBase => ConflictsDialog.Base,
            _ => null,
        };
        if (side is not null)
        {
            await SelectSideAsync(item.Filename, side);
        }
    }

    private string ApplyToAllText(ConflictData item, int unsolved)
        => unsolved > 1
            ? string.Format(UpstreamTranslation.ByText(ConflictsDialog.SolveMergeConflictApplyToAll), item.Filename,
                unsolved - 1)
            : "";

    // Upstream's OpenSolveMergeConflictDialogAndExecuteSelectedMergeAction: the "Solve merge conflict" task dialog with its
    // three command links, unless "apply to all" was checked before, which repeats the last choice.
    private async Task<ConflictChoice> SolveAsync(string text, string applyToAllText, string local, string remote,
        string keepBase)
    {
        if (_applyToAll)
        {
            return _lastChoice;
        }

        System.Windows.Forms.TaskDialogCommandLinkButton localButton = new(local);
        System.Windows.Forms.TaskDialogCommandLinkButton remoteButton = new(remote);
        System.Windows.Forms.TaskDialogCommandLinkButton baseButton = new(keepBase);
        System.Windows.Forms.TaskDialogPage page = new()
        {
            Text = text,
            Heading = "",
            Caption = UpstreamTranslation.ByText(ConflictsDialog.SolveMergeConflictCaption),
            Verification = applyToAllText.Length > 0
                ? new System.Windows.Forms.TaskDialogVerificationCheckBox { Text = applyToAllText }
                : null,
        };
        page.Buttons.Add(System.Windows.Forms.TaskDialogButton.Cancel);
        page.Buttons.Add(localButton);
        page.Buttons.Add(remoteButton);
        page.Buttons.Add(baseButton);
        MessageBoxWindow dialog = new(page);
        await dialog.ShowDialog(this);
        _applyToAll = page.Verification?.Checked ?? false;
        _lastChoice = dialog.ChosenButton == localButton ? ConflictChoice.KeepLocal
            : dialog.ChosenButton == remoteButton ? ConflictChoice.KeepRemote
            : dialog.ChosenButton == baseButton ? ConflictChoice.KeepBase
            : ConflictChoice.None;
        return _lastChoice;
    }

    // Upstream's ContextChooseLocal/Remote/Base_Click: each selected file after its check for that side.
    private async Task ChooseAsync(string side)
    {
        IReadOnlyList<ConflictData> selected = Selected();
        if (selected.Count == 0)
        {
            return;
        }

        Count(selected);
        foreach (ConflictData item in selected)
        {
            bool canChoose = side switch
            {
                ConflictsDialog.Local => await CheckForLocalRevisionAsync(item),
                ConflictsDialog.Remote => await CheckForRemoteRevisionAsync(item),
                _ => await CheckForBaseRevisionAsync(item),
            };
            if (canChoose)
            {
                await SelectSideAsync(side == ConflictsDialog.Base ? item.Base.Filename : item.Filename, side);
            }

            Progress.Value++;
        }

        Progress.IsVisible = false;
        await InitializeAsync();
    }

    // Upstream's HandleConflictSelectSide, with its failure message per side.
    private async Task SelectSideAsync(string fileName, string side)
    {
        if (!await Task.Run(() => KeepCurrentDirectory(() => _module.HandleConflictSelectSide(fileName, side))))
        {
            await ShowMessageAsync(UpstreamTranslation.ByText(side switch
            {
                ConflictsDialog.Local => ConflictsDialog.ChooseLocalFileFailed,
                ConflictsDialog.Remote => ConflictsDialog.ChooseRemoteFileFailed,
                _ => ConflictsDialog.ChooseBaseFileFailed,
            }), Error);
        }
    }

    private Task RemoveFileAsync(string fileName)
        => Task.Run(() => _module.GitExecutable.GetOutput(new GitArgumentBuilder("rm") { "--", fileName.QuoteNE() }));

    private async Task MarkAsSolvedAsync()
    {
        foreach (ConflictData item in Selected())
        {
            await StageFileAsync(item.Filename);
        }

        await InitializeAsync();
    }

    // Upstream's StageFile: git add, with git's output shown when it says something.
    private async Task StageFileAsync(string fileName)
    {
        string output = await Task.Run(() =>
            _module.GitExecutable.GetOutput(new GitArgumentBuilder("add") { "--", fileName.QuoteNE() }));
        if (!string.IsNullOrWhiteSpace(output))
        {
            await new ErrorWindow(string.Format(UpstreamTranslation.ByText(ConflictsDialog.StageFilename), fileName)
                                  + Environment.NewLine + output).ShowDialog(this);
        }
    }

    // Upstream's customMergetool_Click without a tool: git mergetool on each selected file.
    private async Task RunGitMergeToolOnSelectedAsync()
    {
        foreach (ConflictData item in Selected())
        {
            await _actions.RunMergeToolAsync(_repositoryPath, item.Filename);
            await InitializeAsync();
        }
    }

    // Upstream's Mergetool_Click ("Start mergetool"): git mergetool on every conflicted file.
    private async Task StartMergeToolAsync()
    {
        await _actions.RunMergeToolAsync(_repositoryPath, "");
        await InitializeAsync();
    }

    // Upstream's Reset_Click: two questions (the second one can be turned off), then a hard reset and close.
    private async Task ResetAsync()
    {
        if (await AskAsync(UpstreamTranslation.ByText(ConflictsDialog.AbortCurrentOperation),
                UpstreamTranslation.ByText(ConflictsDialog.ResetCaption), MessageBoxButtons.YesNo) == DialogResult.Yes
            && await ConfirmWindow.AskAsync(this, _preferences, Confirmation.SecondAbortConfirmation,
                UpstreamTranslation.ByText(ConflictsDialog.AreYouSureYouWantDeleteFiles), "Yes",
                UpstreamTranslation.ByText(ConflictsDialog.AreYouSureYouWantDeleteFilesCaption)))
        {
            await _actions.ResetAsync(_repositoryPath, "HEAD", ResetMode.Hard);
            Close();
        }
    }

    // Upstream's OpenSideWith: the side saved to a temporary file, then opened.
    private async Task OpenSideWithAsync(string side)
    {
        if (Selected() is not [{ } item])
        {
            return;
        }

        string fileName = Path.Combine(Path.GetTempPath(), PathUtil.GetFileName(item.Filename));
        if (!await Task.Run(() => KeepCurrentDirectory(() => _module.HandleConflictsSaveSide(item.Filename, fileName, side))))
        {
            await ShowMessageAsync(UpstreamTranslation.ByText(ConflictsDialog.FailureWhileOpenFile), Error);
        }

        await LaunchAsync(fileName);
    }

    // Upstream's SaveAs: the side saved where the user chooses.
    private async Task SaveSideAsync(string side)
    {
        if (Selected() is not [{ } item])
        {
            return;
        }

        string fullPath = FullPath(item.Filename);
        IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(
            Path.GetDirectoryName(fullPath) ?? _repositoryPath);
        IStorageFile? target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = PathUtil.GetFileName(item.Filename),
            DefaultExtension = Path.GetExtension(fullPath),
            SuggestedStartLocation = folder,
        });
        if (target?.TryGetLocalPath() is { } path
            && !await Task.Run(() => KeepCurrentDirectory(() => _module.HandleConflictsSaveSide(item.Filename, path, side))))
        {
            await ShowMessageAsync(UpstreamTranslation.ByText(ConflictsDialog.FailureWhileSaveFile), Error);
        }
    }

    // Upstream's "Open" and "Open With": the working tree file in its program.
    private async Task OpenFileAsync()
    {
        if (Selected() is [{ } item])
        {
            await LaunchAsync(FullPath(item.Filename));
        }
    }

    private async Task LaunchAsync(string path)
    {
        if (GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
        }
    }

    // Upstream's hotkeys (FormMergeConflicts) and the Choose items' Ctrl+1, Ctrl+2 and Ctrl+3.
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        Func<Task>? action = Hotkeys.Conflicts.Match(e) switch
        {
            ConflictsCommand.Merge => OpenMergeToolAsync,
            ConflictsCommand.Rescan => InitializeAsync,
            ConflictsCommand.ChooseLocal => () => ChooseAsync(ConflictsDialog.Local),
            ConflictsCommand.ChooseRemote => () => ChooseAsync(ConflictsDialog.Remote),
            ConflictsCommand.ChooseBase => () => ChooseAsync(ConflictsDialog.Base),
            _ => null,
        };
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            action = e.Key switch
            {
                Key.D1 => () => ChooseAsync(ConflictsDialog.Local),
                Key.D2 => () => ChooseAsync(ConflictsDialog.Remote),
                Key.D3 => () => ChooseAsync(ConflictsDialog.Base),
                _ => action,
            };
        }
        else if (e.Key == Key.Enter && ConflictList.IsKeyboardFocusWithin)
        {
            action = OpenMergeToolAsync;
        }

        if (action is not null)
        {
            e.Handled = true;
            Run(action);
        }
    }

    private enum ItemType
    {
        File,
        Directory,
        Submodule,
    }

    // Upstream's GetItemType.
    private ItemType ItemTypeOf(string fileName)
    {
        string fullName = FullPath(fileName);
        return Directory.Exists(fullName) && !File.Exists(fullName)
            ? _module.IsSubmodule(fileName.Trim()) ? ItemType.Submodule : ItemType.Directory
            : ItemType.File;
    }

    // Upstream's conflict methods set the process's current directory to the working directory, which would leave later
    // git calls and the repository folder tied to it; the directory before is put back.
    private static T KeepCurrentDirectory<T>(Func<T> work)
    {
        string current = Directory.GetCurrentDirectory();
        try
        {
            return work();
        }
        finally
        {
            Directory.SetCurrentDirectory(current);
        }
    }

    private string FullPath(string fileName) => Path.Combine(_repositoryPath, fileName.ToNativePath());

    private IReadOnlyList<ConflictData> Selected()
        => [.. ConflictList.SelectedItems?.OfType<ConflictRow>().Select(row => row.Data) ?? []];

    private async Task<DialogResult> AskAsync(string text, string caption, MessageBoxButtons buttons,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        MessageBoxWindow box = new(text, caption, buttons, defaultButton);
        await box.ShowDialog(this);
        return box.Result;
    }

    private Task ShowMessageAsync(string text, string caption) => AskAsync(text, caption, MessageBoxButtons.OK);

    private void Run(Func<Task> action)
        => _work = RunAsync(action);

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await new ErrorWindow(ex.Message).ShowDialog(this);
        }
    }
}

/// <summary>
///  A row of the conflicts list: the conflicted file by its name.
/// </summary>
public sealed record ConflictRow(ConflictData Data)
{
    public override string ToString() => Data.Filename;
}
