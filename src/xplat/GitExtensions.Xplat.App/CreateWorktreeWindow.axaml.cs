using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCreateWorktree</c> in the new shell (rules in <see cref="CreateWorktreeDialog"/>): checks out an
///  existing branch or a new one in a new worktree. Closes with the new worktree's directory, or null.
/// </summary>
public partial class CreateWorktreeWindow : Window
{
    private const string UseRelativePathsKey = "worktree.useRelativePaths";

    private readonly string _repositoryPath;
    private readonly string _mainWorktreePath;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CreateWorktreeDialog? _dialog;
    private bool _useRelativePathsSet;
    private bool _updating;

    /// <param name="mainWorktreePath">The main worktree's folder, which the proposed directory is named after.</param>
    public CreateWorktreeWindow(string repositoryPath, string mainWorktreePath, RepositoryOperationsViewModel actions,
        IAppPreferences preferences)
    {
        _repositoryPath = repositoryPath;
        _mainWorktreePath = mainWorktreePath;
        _actions = actions;
        _preferences = preferences;
        InitializeComponent();
        BranchesBox.PlaceholderText = UpstreamTranslation.ByText(CreateWorktreeDialog.LoadingData);
        CreateButton.IsEnabled = false;
        ExistingBranchRadio.IsCheckedChanged += (_, _) => UpdateDirectoryAndValidate();
        NewBranchRadio.IsCheckedChanged += (_, _) => UpdateDirectoryAndValidate();
        BranchesBox.SelectionChanged += (_, _) => UpdateDirectoryAndValidate();
        BranchesBox.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Run(CreateAsync);
            }
        };
        NewBranchNameBox.TextChanged += (_, _) => UpdateDirectoryAndValidate();
        NewBranchNameBox.LostFocus += (_, _) => NormaliseNewBranchName();
        DirectoryBox.TextChanged += (_, _) => Validate();
        BrowseButton.Click += (_, _) => Run(BrowseAsync);
        CreateButton.Click += (_, _) => Run(CreateAsync);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  The dialog state once the branches are loaded, for tests.
    /// </summary>
    public CreateWorktreeDialog? Dialog => _dialog;

    /// <summary>
    ///  Completes when the branches are shown, for tests.
    /// </summary>
    public Task BranchesLoaded => _loaded.Task;

    private void Run(Func<Task> action) =>
        UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's FormCreateWorktree_Load: the local branches but the current one; with none, a new branch.
    private async Task LoadAsync()
    {
        (IReadOnlyList<string> branches, string current, bool useRelativePathsSet) = await Task.Run(() =>
        {
            GitModule module = GitModules.Open(_repositoryPath);
            return ((IReadOnlyList<string>)[.. module.GetRefs(RefsFilter.Heads).Select(head => head.Name)],
                module.GetSelectedBranch(), !string.IsNullOrEmpty(module.GetEffectiveSetting(UseRelativePathsKey)));
        });
        _useRelativePathsSet = useRelativePathsSet;
        _dialog = new CreateWorktreeDialog(_mainWorktreePath, branches, current);
        _updating = true;
        BranchesBox.PlaceholderText = null;
        BranchesBox.ItemsSource = _dialog.Branches;
        BranchesBox.SelectedItem = _dialog.SelectedBranch;
        ExistingBranchRadio.IsEnabled = _dialog.CanCheckoutExistingBranch;
        ExistingBranchRadio.IsChecked = !_dialog.CreateNewBranch;
        NewBranchRadio.IsChecked = _dialog.CreateNewBranch;
        _updating = false;
        UpdateDirectoryAndValidate();
        _loaded.TrySetResult();
    }

    // Upstream's UpdateWorktreePathAndValidateWorktreeOptions: the proposed directory follows the branch.
    private void UpdateDirectoryAndValidate()
    {
        if (_dialog is null || _updating)
        {
            return;
        }

        ReadControls();
        DirectoryBox.Text = _dialog.ProposedDirectory();
        Validate();
    }

    private void Validate()
    {
        if (_dialog is null || _updating)
        {
            return;
        }

        ReadControls();
        _dialog.Directory = DirectoryBox.Text ?? "";
        BranchesBox.IsEnabled = ExistingBranchRadio.IsChecked == true;
        NewBranchNameBox.IsEnabled = NewBranchRadio.IsChecked == true;
        CreateButton.IsEnabled = _dialog.CanCreate();
    }

    private void ReadControls()
    {
        _dialog!.CreateNewBranch = NewBranchRadio.IsChecked == true;
        _dialog.SelectedBranch = BranchesBox.SelectedItem as string;
        _dialog.NewBranchName = NewBranchNameBox.Text ?? "";
    }

    // Upstream's txtNewBranchName_Leave: the name normalised when "Auto normalise branch name" is on. WinForms raises
    // TextChanged at once, which proposes the directory again; Avalonia raises it later, so that is done here.
    private void NormaliseNewBranchName()
    {
        string text = NewBranchNameBox.Text ?? "";
        string normalised = BranchNames.Normalise(text, _preferences.AutoNormaliseBranchName,
            _preferences.AutoNormaliseSymbol);
        if (normalised != text)
        {
            int caret = NewBranchNameBox.CaretIndex;
            NewBranchNameBox.Text = normalised;
            NewBranchNameBox.CaretIndex = Math.Min(caret, normalised.Length);
            UpdateDirectoryAndValidate();
        }
    }

    // Upstream's FolderBrowserButton: picks a folder for the directory box.
    private async Task BrowseAsync()
    {
        string? text = DirectoryBox.Text;
        IStorageFolder? start = string.IsNullOrWhiteSpace(text) || !Directory.Exists(text)
            ? null
            : await StorageProvider.TryGetFolderFromPathAsync(text);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            DirectoryBox.Text = path;
        }
    }

    // Upstream's CreateWorktree: the new branch's name normalised first, as the accept button skips Leave.
    private async Task CreateAsync()
    {
        if (_dialog is null)
        {
            return;
        }

        if (NewBranchRadio.IsChecked == true)
        {
            NormaliseNewBranchName();
        }

        Validate();
        if (!_dialog.CanCreate())
        {
            return;
        }

        if (await _actions.CreateWorktreeAsync(_repositoryPath, _dialog.Directory,
                _dialog.Arguments(_repositoryPath, _useRelativePathsSet)))
        {
            Close(_dialog.Directory);
            return;
        }

        await new ErrorWindow(_actions.ErrorMessage ?? "").ShowDialog(this);
        _actions.ClearError();
    }
}
