using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormCreateWorktree</c> (rules in <see cref="CreateWorktreeDialog"/>): checks out an
///  existing branch or a new one in a new worktree, whose directory is proposed from the branch. Closes its window with
///  the new worktree's directory.
/// </summary>
public sealed partial class CreateWorktreeViewModel : DialogViewModel
{
    private const string UseRelativePathsKey = "worktree.useRelativePaths";

    private readonly string _repositoryPath;
    private readonly string _mainWorktreePath;
    private readonly IGitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _useRelativePathsSet;
    private bool _updating = true;

    /// <param name="repositoryPath">The repository the worktree is added to.</param>
    /// <param name="mainWorktreePath">The main worktree's folder, which the proposed directory is named after.</param>
    /// <param name="module">The repository, for its branches and settings.</param>
    /// <param name="actions">Runs <c>git worktree add</c>.</param>
    /// <param name="preferences">Where "Auto normalise branch name" and its symbol are kept.</param>
    public CreateWorktreeViewModel(IDialogHost host, string repositoryPath, string mainWorktreePath, IGitModule module,
        RepositoryOperationsViewModel actions, IAppPreferences preferences)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _mainWorktreePath = mainWorktreePath;
        _module = module;
        _actions = actions;
        _preferences = preferences;
        LoadingText = Host.Translate(CreateWorktreeDialog.LoadingData);
    }

    /// <summary>
    ///  The dialog state once the branches are loaded, for tests.
    /// </summary>
    public CreateWorktreeDialog? Dialog { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Branches { get; private set; } = [];

    [ObservableProperty]
    public partial string? SelectedBranch { get; set; }

    /// <summary>
    ///  Upstream's "Checkout an existing branch" is enabled: there is another branch.
    /// </summary>
    [ObservableProperty]
    public partial bool CanCheckoutExistingBranch { get; private set; }

    /// <summary>
    ///  Upstream's "Create a new branch (from current commit)" instead of an existing branch.
    /// </summary>
    [ObservableProperty]
    public partial bool CreateNewBranch { get; set; }

    [ObservableProperty]
    public partial string? NewBranchName { get; set; }

    [ObservableProperty]
    public partial string? Directory { get; set; }

    /// <summary>
    ///  The branch list's placeholder while the branches are read; null once they are listed.
    /// </summary>
    [ObservableProperty]
    public partial string? LoadingText { get; private set; }

    /// <summary>
    ///  Picks a folder starting in the given one (null for none); null when cancelled. The window sets it, as it is the
    ///  platform's picker.
    /// </summary>
    public Func<string?, Task<string?>>? PickFolder { get; set; }

    /// <summary>
    ///  Completes when the branches are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    private bool CanCreate => Dialog?.CanCreate() == true;

    /// <summary>
    ///  Upstream's <c>FormCreateWorktree_Load</c>: the local branches but the current one; with none, a new branch. The
    ///  window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        (IReadOnlyList<string> branches, string current, bool useRelativePathsSet) = await Task.Run(() =>
            ((IReadOnlyList<string>)[.. _module.GetRefs(RefsFilter.Heads).Select(head => head.Name)],
                _module.GetSelectedBranch(), !string.IsNullOrEmpty(_module.GetEffectiveSetting(UseRelativePathsKey))));
        _useRelativePathsSet = useRelativePathsSet;
        CreateWorktreeDialog dialog = new(_mainWorktreePath, branches, current);
        Dialog = dialog;
        _updating = true;
        LoadingText = null;
        Branches = dialog.Branches;
        SelectedBranch = dialog.SelectedBranch;
        CanCheckoutExistingBranch = dialog.CanCheckoutExistingBranch;
        CreateNewBranch = dialog.CreateNewBranch;
        _updating = false;
        UpdateDirectoryAndValidate();
        _loaded.TrySetResult();
    });

    /// <summary>
    ///  Upstream's <c>txtNewBranchName_Leave</c>: the name normalised when "Auto normalise branch name" is on.
    /// </summary>
    public void NormaliseNewBranchName()
        => NewBranchName = BranchNames.Normalise(NewBranchName ?? "", _preferences.AutoNormaliseBranchName,
            _preferences.AutoNormaliseSymbol);

    partial void OnSelectedBranchChanged(string? value) => UpdateDirectoryAndValidate();

    partial void OnCreateNewBranchChanged(bool value) => UpdateDirectoryAndValidate();

    partial void OnNewBranchNameChanged(string? value) => UpdateDirectoryAndValidate();

    partial void OnDirectoryChanged(string? value) => Validate();

    // Upstream's FolderBrowserButton: picks a folder for the directory box.
    [RelayCommand]
    private Task BrowseAsync() => GuardAsync(async () =>
    {
        string? start = string.IsNullOrWhiteSpace(Directory) || !System.IO.Directory.Exists(Directory) ? null : Directory;
        if (PickFolder is not null && await PickFolder(start) is { } path)
        {
            Directory = path;
        }
    });

    // Upstream's CreateWorktree: the new branch's name normalised first, as the accept button skips Leave.
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private Task CreateAsync() => GuardAsync(async () =>
    {
        if (Dialog is not { } dialog)
        {
            return;
        }

        if (CreateNewBranch)
        {
            NormaliseNewBranchName();
        }

        Validate();
        if (!dialog.CanCreate())
        {
            return;
        }

        if (await _actions.CreateWorktreeAsync(_repositoryPath, dialog.Directory,
                dialog.Arguments(_repositoryPath, _useRelativePathsSet)))
        {
            Host.Close(dialog.Directory);
            return;
        }

        Host.ShowError(new InvalidOperationException(_actions.ErrorMessage ?? ""));
        _actions.ClearError();
    });

    // Upstream's UpdateWorktreePathAndValidateWorktreeOptions: the proposed directory follows the branch.
    private void UpdateDirectoryAndValidate()
    {
        if (Dialog is null || _updating)
        {
            return;
        }

        ReadState();
        Directory = Dialog.ProposedDirectory();
        Validate();
    }

    private void Validate()
    {
        if (Dialog is null || _updating)
        {
            return;
        }

        ReadState();
        Dialog.Directory = Directory ?? "";
        CreateCommand.NotifyCanExecuteChanged();
    }

    private void ReadState()
    {
        Dialog!.CreateNewBranch = CreateNewBranch;
        Dialog.SelectedBranch = SelectedBranch;
        Dialog.NewBranchName = NewBranchName ?? "";
    }
}
