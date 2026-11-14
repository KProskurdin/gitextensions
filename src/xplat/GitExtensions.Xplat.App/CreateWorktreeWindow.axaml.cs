using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCreateWorktree</c> in the new shell, bound to <see cref="CreateWorktreeViewModel"/> (rules in
///  <see cref="CreateWorktreeDialog"/>): checks out an existing branch or a new one in a new worktree. Closes with the new
///  worktree's directory, or null.
/// </summary>
public partial class CreateWorktreeWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CreateWorktreeWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="mainWorktreePath">The main worktree's folder, which the proposed directory is named after.</param>
    public CreateWorktreeWindow(string repositoryPath, string mainWorktreePath, RepositoryOperationsViewModel actions,
        IAppPreferences preferences)
    {
        ViewModel = new CreateWorktreeViewModel(new WindowDialogHost(this), repositoryPath, mainWorktreePath,
            GitModules.Open(repositoryPath), actions, preferences)
        {
            PickFolder = PickFolderAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this);

        // View concerns: Enter in the branch list creates, and the new branch's name is normalised when its box is left,
        // keeping the caret where it was.
        BranchesBox.KeyUp += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.CreateCommand.Execute(null);
            }
        };
        NewBranchNameBox.LostFocus += (_, _) =>
        {
            int caret = NewBranchNameBox.CaretIndex;
            ViewModel.NormaliseNewBranchName();
            NewBranchNameBox.CaretIndex = Math.Min(caret, ViewModel.NewBranchName?.Length ?? 0);
        };
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public CreateWorktreeViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state once the branches are loaded, for tests.
    /// </summary>
    public CreateWorktreeDialog? Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the branches are shown, for tests.
    /// </summary>
    public Task BranchesLoaded => ViewModel.Loaded;

    private async Task<string?> PickFolderAsync(string? startPath)
    {
        IStorageFolder? start = startPath is null ? null : await StorageProvider.TryGetFolderFromPathAsync(startPath);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
