using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormStash</c> (rules in <see cref="StashDialog"/>): the working directory's changes
///  and the stashes, the selected one's files, a new stash of all or the selected changes, and drop and apply of the
///  selected stash. "Keep index" and "Include untracked files" are upstream's settings, kept on close. The file list, its
///  menu and the diff are the view's.
/// </summary>
public sealed partial class StashViewModel : DialogViewModel
{
    private readonly string _repositoryPath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly Func<Task<bool>> _confirmDrop;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _showing;
    private int _filesVersion;

    /// <param name="actions">Runs the stash commands.</param>
    /// <param name="preferences">Upstream's "Keep index" and "Include untracked files".</param>
    /// <param name="manageStashes">Upstream's <c>ManageStashes</c>: show the newest stash first.</param>
    /// <param name="initialStash">The stash to show first ("stash@{n}").</param>
    /// <param name="confirmDrop">Upstream's drop question, which can be turned off.</param>
    public StashViewModel(IDialogHost host, string repositoryPath, RepositoryOperationsViewModel actions,
        IAppPreferences preferences, bool manageStashes, string? initialStash, Func<Task<bool>> confirmDrop)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _actions = actions;
        _preferences = preferences;
        _confirmDrop = confirmDrop;
        Dialog = new StashDialog(manageStashes, initialStash);
        KeepIndex = preferences.StashKeepIndex;
        IncludeUntracked = preferences.IncludeUntrackedFilesInManualStash;
    }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public StashDialog Dialog { get; }

    /// <summary>
    ///  Completes when the stashes and the first stash's files are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  The working directory's changes and the stashes, as upstream's "Show:" box lists them.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<StashChoice> Stashes { get; private set; } = [];

    [ObservableProperty]
    public partial int SelectedStashIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string? Message { get; set; }

    [ObservableProperty]
    public partial bool IsMessageReadOnly { get; private set; }

    [ObservableProperty]
    public partial bool CanApplyOrDrop { get; private set; }

    [ObservableProperty]
    public partial bool KeepIndex { get; set; }

    [ObservableProperty]
    public partial bool IncludeUntracked { get; set; }

    /// <summary>
    ///  True while the selected stash's files are read.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoadingFiles { get; private set; }

    /// <summary>
    ///  The selected stash's files; null while they are read.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<StashFile>? Files { get; private set; }

    /// <summary>
    ///  The revisions the shown files are compared between.
    /// </summary>
    public StashRevisions? Revisions { get; private set; }

    /// <summary>
    ///  The files selected in the view's list; the view sets it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStashSelected))]
    public partial IReadOnlyList<StashFile> SelectedFiles { get; set; } = [];

    public bool CanStashSelected => Dialog.CanStashSelected(SelectedFiles.Count);

    /// <summary>
    ///  Upstream's <c>Initialize</c>: the stashes read again, then the chosen one shown. The window calls it when it opens
    ///  and the file list's menu after a change.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            await ReloadAsync();
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    /// <summary>
    ///  Upstream's next and previous stash hotkeys, while no files are read.
    /// </summary>
    public void SelectNeighbour(bool next)
    {
        if (!IsLoadingFiles && Dialog.SelectNeighbour(next))
        {
            SelectedStashIndex = Dialog.SelectedIndex;
        }
    }

    /// <summary>
    ///  As upstream's <c>RefreshAll</c>: only the working directory's changes can have changed.
    /// </summary>
    public void RefreshWorkingDirectory()
    {
        if (Dialog.IsWorkingDirectorySelected)
        {
            Run(ReloadAsync);
        }
    }

    /// <summary>
    ///  Keeps "Keep index" and "Include untracked files"; the window calls it when it closes.
    /// </summary>
    public void SaveOptions()
    {
        _preferences.StashKeepIndex = KeepIndex;
        _preferences.IncludeUntrackedFilesInManualStash = IncludeUntracked;
        _preferences.Save();
    }

    partial void OnSelectedStashIndexChanged(int value)
    {
        if (!_showing && value >= 0)
        {
            Dialog.Select(value);
            Run(ShowSelectedStashAsync);
        }
    }

    private async Task ReloadAsync()
    {
        IReadOnlyList<GitStash> stashes =
            await Task.Run(() => GitModules.Open(_repositoryPath).GetStashes(noLocks: false));
        Dialog.Load(stashes);
        _showing = true;
        Stashes =
        [
            .. Dialog.Items.Select(stash =>
                new StashChoice(stash, stash.Index == -1 ? Host.Translate(stash.Message) : stash.Summary))
        ];
        SelectedStashIndex = Dialog.SelectedIndex;
        _showing = false;
        await ShowSelectedStashAsync();
    }

    // Upstream's StashesSelectedIndexChanged and InitializeSoft: the message, the buttons and the files of the selection.
    private async Task ShowSelectedStashAsync()
    {
        if (Dialog.Selected is not { } stash)
        {
            return;
        }

        int version = ++_filesVersion;
        Message = Host.Translate(Dialog.Message);
        IsMessageReadOnly = Dialog.IsMessageReadOnly;
        CanApplyOrDrop = Dialog.CanApplyOrDrop;
        Files = null;
        IsLoadingFiles = true;
        try
        {
            bool workingDirectory = Dialog.IsWorkingDirectorySelected;
            (IReadOnlyList<StashFile> files, StashRevisions revisions) = await Task.Run(() =>
            {
                GitModule module = GitModules.Open(_repositoryPath);
                return (Dialog.Files(module, stash), StashRevisions.Read(module, stash, workingDirectory));
            });
            if (version != _filesVersion)
            {
                return;
            }

            Revisions = revisions;
            Files = files;
        }
        finally
        {
            if (version == _filesVersion)
            {
                IsLoadingFiles = false;
                OnPropertyChanged(nameof(CanStashSelected));
            }
        }
    }

    // Upstream's StashClick and StashSelectedFiles_Click: a new stash, then the list read again.
    [RelayCommand]
    private Task StashAllAsync() => GuardAsync(() => StashAsync(paths: null));

    [RelayCommand]
    private Task StashSelectedAsync() => GuardAsync(async () =>
    {
        List<string> paths = [.. SelectedFiles.Select(file => file.Change.Path).Distinct()];
        if (paths.Count > 0)
        {
            await StashAsync(paths);
        }
    });

    private async Task StashAsync(IReadOnlyList<string>? paths)
    {
        await _actions.StashAsync(_repositoryPath, StashDialog.MessageArgument(Message), IncludeUntracked, KeepIndex,
            paths);
        await ReloadAsync();
    }

    // Upstream's ClearClick: asks first (unless "Don't show me this message again" was chosen), and shows the stash that
    // takes the dropped one's place.
    [RelayCommand]
    private Task DropAsync() => GuardAsync(async () =>
    {
        if (Dialog.Selected is not { } stash || !Dialog.CanApplyOrDrop || !await _confirmDrop())
        {
            return;
        }

        Dialog.Dropping();
        await _actions.DropStashAsync(_repositoryPath, stash.Name);
        await ReloadAsync();
    });

    [RelayCommand]
    private Task ApplyAsync() => GuardAsync(async () =>
    {
        if (Dialog.Selected is { } stash && Dialog.CanApplyOrDrop)
        {
            await _actions.ApplyStashAsync(_repositoryPath, stash.Name);
            await ReloadAsync();
        }
    });
}

/// <summary>
///  An entry of the stash window's "Show:" box: upstream's <c>GitStash.Summary</c>.
/// </summary>
public sealed record StashChoice(GitStash Stash, string Text);

/// <summary>
///  The revisions upstream's <c>LoadGitItemStatuses</c> gives the shown files: HEAD for the working directory, the stash
///  and the commit it was made on for a stash.
/// </summary>
public sealed record StashRevisions(bool IsWorkingDirectory, ObjectId Head, ObjectId StashParent, ObjectId Stash)
{
    public static StashRevisions Read(GitModule module, GitStash stash, bool workingDirectory)
        => workingDirectory
            ? new StashRevisions(true, module.RevParse("HEAD"), default, default)
            : new StashRevisions(false, default, module.RevParse(stash.Name + "^"), module.RevParse(stash.Name));

    /// <summary>
    ///  The revisions a file's change is shown between, for the file list's menu.
    /// </summary>
    public DiffMenuRevisions Of(FileChange change)
    {
        if (!IsWorkingDirectory)
        {
            return new DiffMenuRevisions(StashParent.IsZero ? null : StashParent, Stash,
                StashParent.IsZero ? [] : [StashParent]);
        }

        // Without HEAD (an unborn branch) upstream lists every file against the working directory.
        return change.Staged && !Head.IsZero
            ? new DiffMenuRevisions(Head, ObjectId.IndexId, [Head])
            : new DiffMenuRevisions(Head.IsZero ? null : ObjectId.IndexId, ObjectId.WorkTreeId, [ObjectId.IndexId]);
    }
}
