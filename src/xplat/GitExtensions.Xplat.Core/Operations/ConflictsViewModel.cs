using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogCommandLinkButton = System.Windows.Forms.TaskDialogCommandLinkButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;
using TaskDialogVerificationCheckBox = System.Windows.Forms.TaskDialogVerificationCheckBox;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  A row of the conflicts list: the conflicted file by its name.
/// </summary>
public sealed record ConflictRow(ConflictData Data)
{
    public override string ToString() => Data.Filename;
}

/// <summary>
///  What <see cref="ConflictsViewModel"/> asks of its window and the app besides <see cref="IDialogHost"/>: the windows it
///  opens, the platform's pickers and programs, and the reads tests replace.
/// </summary>
public sealed record ConflictsViewModelServices
{
    /// <summary>
    ///  Reads the repository again for the main window (the counts and the action banner).
    /// </summary>
    public required Func<Task> RefreshRepository { get; init; }

    /// <summary>
    ///  Upstream's <c>MessageBoxes.ConfirmSuppressible</c>: the question with its "don't ask again" check box; true at once
    ///  when the question is turned off.
    /// </summary>
    public required Func<Confirmation, string, string, Task<bool>> ConfirmSuppressible { get; init; }

    /// <summary>
    ///  Shows a task dialog page and gives the button chosen, or null when it was closed.
    /// </summary>
    public required Func<TaskDialogPage, Task<TaskDialogButton?>> ShowTaskDialog { get; init; }

    /// <summary>
    ///  Upstream's <c>FormMergeSubmodule</c> on a conflicted submodule: true once it staged the submodule.
    /// </summary>
    public required Func<string, Task<bool>> MergeSubmodule { get; init; }

    /// <summary>
    ///  The save picker: the suggested file name and the folder it starts in; the path chosen, or null.
    /// </summary>
    public required Func<string, string, Task<string?>> PickSaveFile { get; init; }

    /// <summary>
    ///  Opens a file in its program.
    /// </summary>
    public required Func<string, Task> LaunchFile { get; init; }

    public required IFileManager FileManager { get; init; }

    /// <summary>
    ///  Opens the file history window on a file.
    /// </summary>
    public required Action<string> ShowFileHistory { get; init; }

    /// <summary>
    ///  Opens the commit window, as upstream offers once every conflict is resolved.
    /// </summary>
    public Action? ShowCommit { get; init; }

    /// <summary>
    ///  Upstream's <c>UpdateSubmodules</c> once the conflicts are resolved, given the repository's path.
    /// </summary>
    public Func<string, Task<bool>>? UpdateSubmodules { get; init; }

    /// <summary>
    ///  Upstream's <c>offerCommit</c>: false when the commit window opened this one.
    /// </summary>
    public bool OfferCommit { get; init; } = true;

    /// <summary>
    ///  Git's merge tools that are available, for "Open in mergetool".
    /// </summary>
    public Func<Task<IReadOnlyList<string>>> AvailableMergeTools { get; init; }
        = () => Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>
    ///  Reads the configured merge tool; null reads it with git (<see cref="ConflictsDialog.ReadMergeTool"/>).
    /// </summary>
    public Func<MergeTool>? ReadMergeTool { get; init; }
}

/// <summary>
///  The view model of upstream's <c>FormResolveConflicts</c> (rules in <see cref="ConflictsDialog"/>): the unresolved
///  files, merged with the configured merge tool (run with its <c>cmd</c>, or through git mergetool), resolved by choosing a
///  side, deleted or kept when one side deleted them, and opened or saved per side; Rescan and Reset; once the last conflict
///  is resolved it offers to commit and closes its window.
/// </summary>
public sealed partial class ConflictsViewModel : DialogViewModel
{
    private const string Error = "Error";
    private const string Warning = "Warning";
    private const string OpenInMergeTool = "Open in mergetool";
    private const string NoName = "...";

    private readonly IGitModule _module;
    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly ConflictsViewModelServices _services;
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

    public ConflictsViewModel(IDialogHost host, IGitModule module, string repositoryPath,
        RepositoryOperationsViewModel actions, ConflictsViewModelServices services)
        : base(host)
    {
        _module = module;
        _repositoryPath = repositoryPath;
        _actions = actions;
        _services = services;
        OpenInText = host.Translate(OpenInMergeTool);
        ChooseLocalText = host.Translate(ConflictsDialog.ChooseLocalButtonText);
        ChooseRemoteText = host.Translate(ConflictsDialog.ChooseRemoteButtonText);
        LocalLabel = host.Translate(ConflictsDialog.LocalLabel);
        RemoteLabel = host.Translate(ConflictsDialog.RemoteLabel);
        Description = host.Translate(ConflictsDialog.SelectFile);
    }

    /// <summary>
    ///  The unresolved files on show.
    /// </summary>
    public IReadOnlyList<ConflictData> Conflicts { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<ConflictRow> Rows { get; private set; } = [];

    /// <summary>
    ///  The first selected row, kept across a rescan as upstream keeps it.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    /// <summary>
    ///  The selected rows; the list sets them, as it can select several.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<ConflictRow> SelectedRows { get; set; } = [];

    /// <summary>
    ///  Git's merge tools for "Open in mergetool", the configured one first; empty unless there are several.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> MergeTools { get; private set; } = [];

    [ObservableProperty]
    public partial string OpenInText { get; private set; }

    [ObservableProperty]
    public partial string ChooseLocalText { get; private set; }

    [ObservableProperty]
    public partial string? ChooseLocalTooltip { get; private set; }

    [ObservableProperty]
    public partial string ChooseRemoteText { get; private set; }

    [ObservableProperty]
    public partial string? ChooseRemoteTooltip { get; private set; }

    [ObservableProperty]
    public partial string? ChooseBaseTooltip { get; private set; }

    [ObservableProperty]
    public partial string LocalLabel { get; private set; }

    [ObservableProperty]
    public partial string? LocalLabelTooltip { get; private set; }

    [ObservableProperty]
    public partial string RemoteLabel { get; private set; }

    [ObservableProperty]
    public partial string? RemoteLabelTooltip { get; private set; }

    [ObservableProperty]
    public partial string Description { get; private set; }

    [ObservableProperty]
    public partial string BaseName { get; private set; } = NoName;

    [ObservableProperty]
    public partial string LocalName { get; private set; } = NoName;

    [ObservableProperty]
    public partial string RemoteName { get; private set; } = NoName;

    /// <summary>
    ///  Upstream's <c>SetAvailableCommands</c>: the items that work on one file.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpenMergeTool), nameof(CanUseLocal), nameof(CanUseRemote), nameof(CanUseBase))]
    public partial bool HasOneFile { get; private set; }

    /// <summary>
    ///  The app's own run of the merge tool, on one file.
    /// </summary>
    public bool CanOpenMergeTool => HasOneFile && _tool.HasExtras;

    /// <summary>
    ///  Upstream's <c>ConflictedFilesContextMenu_Opening</c>: a side the file does not have cannot be opened or saved.
    /// </summary>
    public bool CanUseLocal => SelectedRows is [{ } row] && ConflictsDialog.HasFile(row.Data.Local);

    public bool CanUseRemote => SelectedRows is [{ } row] && ConflictsDialog.HasFile(row.Data.Remote);

    public bool CanUseBase => SelectedRows is [{ } row] && ConflictsDialog.HasFile(row.Data.Base);

    [ObservableProperty]
    public partial bool IsProgressVisible { get; private set; }

    [ObservableProperty]
    public partial int ProgressMaximum { get; private set; }

    [ObservableProperty]
    public partial int ProgressValue { get; private set; }

    /// <summary>
    ///  Completes when the merge tool is read and the files are listed, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>OnRuntimeLoad</c> and <c>FormResolveConflicts_Load</c>: whether there were conflicts, the merge tool,
    ///  the list, then git's merge tools. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            _thereWereConflicts = await Task.Run(() => _module.InTheMiddleOfConflictedMerge());
            _tool = await Task.Run(_services.ReadMergeTool ?? ReadMergeTool);
            if (_tool.Error is { } error)
            {
                await Host.ShowMessageAsync(error, _tool.Name is null ? Error : Warning);
            }

            await InitializeAsync();

            // Upstream's LoadCustomMergetools: the list only when there is a choice.
            IReadOnlyList<string> tools = await _services.AvailableMergeTools();
            if (tools.Count > 1)
            {
                MergeTools = tools;
            }
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    partial void OnSelectedRowsChanged(IReadOnlyList<ConflictRow> value) => ShowSelection();

    // Upstream's OpenMergeTool: the files deleted locally first, then those deleted remotely, then the rest, each through
    // the merge tool or the question for its case.
    [RelayCommand]
    private Task MergeAsync() => GuardAsync(async () =>
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
            foreach (IReadOnlyList<ConflictData> group in new[] { deletedLocally, deletedRemotely, remaining })
            {
                _applyToAll = false;
                foreach (ConflictData item in group)
                {
                    ProgressValue++;
                    await ResolveItemAsync(item);
                }
            }
        }
        finally
        {
            _applyToAll = false;
            IsProgressVisible = false;
            await InitializeAsync();
        }
    });

    // Upstream's customMergetool_Click: git mergetool on each selected file, with the chosen tool or the configured one.
    [RelayCommand]
    private Task RunGitMergeToolAsync(string? tool) => GuardAsync(async () =>
    {
        foreach (ConflictData item in Selected())
        {
            await _actions.RunMergeToolAsync(_repositoryPath, item.Filename, tool);
            await InitializeAsync();
        }
    });

    // Upstream's Mergetool_Click ("Start mergetool"): git mergetool on every conflicted file.
    [RelayCommand]
    private Task StartMergeToolAsync() => GuardAsync(async () =>
    {
        await _actions.RunMergeToolAsync(_repositoryPath, "");
        await InitializeAsync();
    });

    [RelayCommand]
    private Task RescanAsync() => GuardAsync(InitializeAsync);

    // Upstream's Reset_Click: two questions (the second one can be turned off), then a hard reset and close.
    [RelayCommand]
    private Task ResetAsync() => GuardAsync(async () =>
    {
        if (await Host.ConfirmAsync(Host.Translate(ConflictsDialog.AbortCurrentOperation),
                Host.Translate(ConflictsDialog.ResetCaption))
            && await _services.ConfirmSuppressible(Confirmation.SecondAbortConfirmation,
                Host.Translate(ConflictsDialog.AreYouSureYouWantDeleteFiles),
                Host.Translate(ConflictsDialog.AreYouSureYouWantDeleteFilesCaption)))
        {
            await _actions.ResetAsync(_repositoryPath, "HEAD", ResetMode.Hard);
            Host.Close(null);
        }
    });

    [RelayCommand]
    private Task MarkAsSolvedAsync() => GuardAsync(async () =>
    {
        foreach (ConflictData item in Selected())
        {
            await StageFileAsync(item.Filename);
        }

        await InitializeAsync();
    });

    // Upstream's ContextChooseLocal/Remote/Base_Click: each selected file after its check for that side.
    [RelayCommand]
    private Task ChooseAsync(string side) => GuardAsync(async () =>
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

            ProgressValue++;
        }

        IsProgressVisible = false;
        await InitializeAsync();
    });

    // Upstream's OpenSideWith: the side saved to a temporary file, then opened.
    [RelayCommand]
    private Task OpenSideWithAsync(string side) => GuardAsync(async () =>
    {
        if (Selected() is not [{ } item])
        {
            return;
        }

        string fileName = Path.Combine(Path.GetTempPath(), PathUtil.GetFileName(item.Filename));
        if (!await Task.Run(() =>
                KeepCurrentDirectory(() => _module.HandleConflictsSaveSide(item.Filename, fileName, side))))
        {
            await Host.ShowMessageAsync(Host.Translate(ConflictsDialog.FailureWhileOpenFile), Error);
        }

        await _services.LaunchFile(fileName);
    });

    // Upstream's SaveAs: the side saved where the user chooses.
    [RelayCommand]
    private Task SaveSideAsync(string side) => GuardAsync(async () =>
    {
        if (Selected() is not [{ } item])
        {
            return;
        }

        string? path = await _services.PickSaveFile(PathUtil.GetFileName(item.Filename),
            Path.GetDirectoryName(FullPath(item.Filename)) ?? _repositoryPath);
        if (path is not null
            && !await Task.Run(() =>
                KeepCurrentDirectory(() => _module.HandleConflictsSaveSide(item.Filename, path, side))))
        {
            await Host.ShowMessageAsync(Host.Translate(ConflictsDialog.FailureWhileSaveFile), Error);
        }
    });

    // Upstream's "Open" and "Open With": the working tree file in its program.
    [RelayCommand]
    private Task OpenFileAsync() => GuardAsync(async () =>
    {
        if (Selected() is [{ } item])
        {
            await _services.LaunchFile(FullPath(item.Filename));
        }
    });

    [RelayCommand]
    private void ShowInFolder()
    {
        if (Selected() is [{ } item])
        {
            _services.FileManager.OpenFolder(Path.GetDirectoryName(FullPath(item.Filename)) ?? _repositoryPath);
        }
    }

    [RelayCommand]
    private void ShowFileHistory()
    {
        if (Selected() is [{ } item])
        {
            _services.ShowFileHistory(item.Filename);
        }
    }

    // Upstream's InitMergetool with git's settings and the installed git.
    private MergeTool ReadMergeTool()
        => ConflictsDialog.ReadMergeTool(key => _module.GetEffectiveSetting(key),
            GitVersion.Current.SupportGuiMergeTool,
            OperatingSystem.IsWindows(),
            (string name, out string? fullPath) => PathUtil.TryFindFullPath(name, out fullPath),
            Host.Translate);

    // Upstream's Initialize: the files (keeping the selected row), the texts for a merge or a rebase, and once no conflict
    // is left the submodule update, the offer to commit and the close.
    private async Task InitializeAsync()
    {
        (bool inRebase, IReadOnlyList<ConflictData> conflicts) = await Task.Run(async () =>
            (_module.InTheMiddleOfRebase(), (IReadOnlyList<ConflictData>)await _module.GetConflictsAsync()));
        int oldIndex = Math.Max(0, SelectedIndex);
        _dialog = new ConflictsDialog(inRebase);
        Conflicts = conflicts;
        Rows = [.. conflicts.Select(conflict => new ConflictRow(conflict))];
        int index = Rows.Count > 0 ? Math.Min(oldIndex, Rows.Count - 1) : -1;
        SelectedIndex = index;
        SelectedRows = index >= 0 ? [Rows[index]] : [];

        OpenInText = $"{Host.Translate(ConflictsDialog.OpenIn)} {_tool.Name}";
        ChooseLocalText = Host.Translate(_dialog.ChooseLocalText);
        ChooseLocalTooltip = Host.Translate(_dialog.ChooseLocalTooltip);
        ChooseRemoteText = Host.Translate(_dialog.ChooseRemoteText);
        ChooseRemoteTooltip = Host.Translate(_dialog.ChooseRemoteTooltip);
        ChooseBaseTooltip = Host.Translate(ConflictsDialog.ChooseBaseTooltip);
        LocalLabel = _dialog.LocalLabelText(Host.Translate(ConflictsDialog.LocalLabel));
        LocalLabelTooltip = Host.Translate(_dialog.LocalLabelTooltip);
        RemoteLabel = _dialog.RemoteLabelText(Host.Translate(ConflictsDialog.RemoteLabel));
        RemoteLabelTooltip = Host.Translate(_dialog.RemoteLabelTooltip);
        ShowSelection();
        await _services.RefreshRepository();

        (bool conflicted, bool inPatch) = await Task.Run(() =>
            (_module.InTheMiddleOfConflictedMerge(), _module.InTheMiddleOfPatch()));
        if (!conflicted && _thereWereConflicts)
        {
            if (_services.UpdateSubmodules is not null)
            {
                await _services.UpdateSubmodules(_repositoryPath);
            }

            if (!inPatch && !inRebase && _services.OfferCommit
                && await _services.ConfirmSuppressible(Confirmation.CommitAfterConflictsResolved,
                    Host.Translate(ConflictsDialog.AllConflictsResolved),
                    Host.Translate(ConflictsDialog.AllConflictsResolvedCaption)))
            {
                _services.ShowCommit?.Invoke();
            }

            Host.Close(null);
        }
    }

    // Upstream's UpdateConflictedFilesMenu: one file shows its description and names; several disable the one-file items.
    private void ShowSelection()
    {
        BaseName = LocalName = RemoteName = "";
        IReadOnlyList<ConflictData> selected = Selected();
        HasOneFile = selected.Count == 1;

        // The sides of another file of one may differ.
        OnPropertyChanged(nameof(CanOpenMergeTool));
        OnPropertyChanged(nameof(CanUseLocal));
        OnPropertyChanged(nameof(CanUseRemote));
        OnPropertyChanged(nameof(CanUseBase));
        if (selected is not [{ } item])
        {
            return;
        }

        if (_dialog.Description(item, Host.Translate) is { } description)
        {
            Description = description;
        }

        (BaseName, LocalName, RemoteName) = ConflictsDialog.Names(item, ItemTypeOf(item.Filename) == ItemType.Submodule,
            Host.Translate);
    }

    // Upstream's GetConflicts: the counts its questions use, and the progress bar.
    private (IReadOnlyList<ConflictData> DeletedLocally, IReadOnlyList<ConflictData> DeletedRemotely,
        IReadOnlyList<ConflictData> Remaining) Count(IReadOnlyList<ConflictData> selected)
    {
        (IReadOnlyList<ConflictData> deletedLocally, IReadOnlyList<ConflictData> deletedRemotely,
            IReadOnlyList<ConflictData> remaining) = ConflictsDialog.Classify(selected);
        _selectedCount = selected.Count;
        _deletedLocallyUnsolved = deletedLocally.Count;
        _deletedRemotelyUnsolved = deletedRemotely.Count;
        _othersSelected = remaining.Count > 0;
        ProgressMaximum = selected.Count;
        ProgressValue = 0;
        IsProgressVisible = true;
        return (deletedLocally, deletedRemotely, remaining);
    }

    // Upstream's ResolveItemConflictAsync: a submodule in upstream's FormMergeSubmodule, staged once that window is done;
    // a file through the merge tool.
    private async Task ResolveItemAsync(ConflictData item)
    {
        switch (ItemTypeOf(item.Filename))
        {
            case ItemType.Submodule:
                if (await _services.MergeSubmodule(item.Filename))
                {
                    await StageFileAsync(item.Filename);
                }

                break;
            case ItemType.File:
                await ResolveFileAsync(item);
                break;
        }
    }

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
                && !await Host.ConfirmAsync(string.Format(Host.Translate(ConflictsDialog.FileIsBinary), _tool.Name),
                    Warning, defaultIsNo: true))
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
                bool? twoWay = await Host.AskAsync(
                    string.Format(Host.Translate(ConflictsDialog.NoBaseRevision), item.Filename),
                    Host.Translate(ConflictsDialog.NoBaseFileMergeCaption));
                if (twoWay is null)
                {
                    return;
                }

                if (twoWay == true)
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
                await Host.ShowMessageAsync(string.Format(Host.Translate(ConflictsDialog.ErrorStartingMergetool),
                    _tool.Path), Host.Translate(ConflictsDialog.NoBaseFileMergeCaption));
                return;
            }

            DateTime after = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : before;
            switch (ConflictsDialog.Outcome(run.ExitCode, after != before))
            {
                case MergeToolOutcome.Stage:
                    await StageFileAsync(item.Filename);
                    break;
                case MergeToolOutcome.Ask when await Host.ConfirmAsync(
                    Host.Translate(ConflictsDialog.AskMergeConflictSolved),
                    Host.Translate(ConflictsDialog.AskMergeConflictSolvedCaption)):
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
            _othersSelected || _deletedRemotelyUnsolved > 0, _applyToAll, Host.Translate);
        (string local, string remote, string baseText) =
            _dialog.DeletedSideButtons(deletedLocally: true, Host.Translate);
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
            _othersSelected || _deletedLocallyUnsolved > 0, _applyToAll, Host.Translate);
        (string local, string remote, string baseText) =
            _dialog.DeletedSideButtons(deletedLocally: false, Host.Translate);
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
            string.Format(Host.Translate(ConflictsDialog.FileCreatedLocallyAndRemotelyLong), item.Filename,
                _dialog.LocalSide, _dialog.RemoteSide), "",
            $"{Host.Translate(ConflictsDialog.ChooseLocalButtonText)} ({_dialog.LocalSide})",
            $"{Host.Translate(ConflictsDialog.ChooseRemoteButtonText)} ({_dialog.RemoteSide})",
            Host.Translate(ConflictsDialog.DeleteFileButtonText));
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
            string.Format(Host.Translate(ConflictsDialog.FileBinaryChooseLocalBaseRemote), item.Local.Filename,
                _dialog.LocalSide, _dialog.RemoteSide), "",
            $"{Host.Translate(ConflictsDialog.ChooseLocalButtonText)} ({_dialog.LocalSide})",
            $"{Host.Translate(ConflictsDialog.ChooseRemoteButtonText)} ({_dialog.RemoteSide})",
            Host.Translate(ConflictsDialog.KeepBaseButtonText));
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
            ? string.Format(Host.Translate(ConflictsDialog.SolveMergeConflictApplyToAll), item.Filename, unsolved - 1)
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

        TaskDialogCommandLinkButton localButton = new(local);
        TaskDialogCommandLinkButton remoteButton = new(remote);
        TaskDialogCommandLinkButton baseButton = new(keepBase);
        TaskDialogPage page = new()
        {
            Text = text,
            Heading = "",
            Caption = Host.Translate(ConflictsDialog.SolveMergeConflictCaption),
            Verification = applyToAllText.Length > 0
                ? new TaskDialogVerificationCheckBox { Text = applyToAllText }
                : null,
        };
        page.Buttons.Add(TaskDialogButton.Cancel);
        page.Buttons.Add(localButton);
        page.Buttons.Add(remoteButton);
        page.Buttons.Add(baseButton);
        TaskDialogButton? chosen = await _services.ShowTaskDialog(page);
        _applyToAll = page.Verification?.Checked ?? false;
        _lastChoice = chosen == localButton ? ConflictChoice.KeepLocal
            : chosen == remoteButton ? ConflictChoice.KeepRemote
            : chosen == baseButton ? ConflictChoice.KeepBase
            : ConflictChoice.None;
        return _lastChoice;
    }

    // Upstream's HandleConflictSelectSide, with its failure message per side.
    private async Task SelectSideAsync(string fileName, string side)
    {
        if (!await Task.Run(() => KeepCurrentDirectory(() => _module.HandleConflictSelectSide(fileName, side))))
        {
            await Host.ShowMessageAsync(Host.Translate(side switch
            {
                ConflictsDialog.Local => ConflictsDialog.ChooseLocalFileFailed,
                ConflictsDialog.Remote => ConflictsDialog.ChooseRemoteFileFailed,
                _ => ConflictsDialog.ChooseBaseFileFailed,
            }), Error);
        }
    }

    private Task RemoveFileAsync(string fileName)
        => Task.Run(() => _module.GitExecutable.GetOutput(new GitArgumentBuilder("rm") { "--", fileName.QuoteNE() }));

    // Upstream's StageFile: git add, with git's output shown when it says something.
    private async Task StageFileAsync(string fileName)
    {
        string output = await Task.Run(() =>
            _module.GitExecutable.GetOutput(new GitArgumentBuilder("add") { "--", fileName.QuoteNE() }));
        if (!string.IsNullOrWhiteSpace(output))
        {
            await Host.ShowMessageAsync(string.Format(Host.Translate(ConflictsDialog.StageFilename), fileName)
                                        + Environment.NewLine + output);
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

    private IReadOnlyList<ConflictData> Selected() => [.. SelectedRows.Select(row => row.Data)];
}
