using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormAddSubmodule</c>, bound to <see cref="AddSubmoduleViewModel"/> (rules in
///  <see cref="SubmodulesDialog"/>): <c>git submodule add</c> of a repository at a local path, on a branch, forced or not.
/// </summary>
public partial class AddSubmoduleWindow : Window
{
    public AddSubmoduleWindow()
        : this("", _ => Task.FromResult(false))
    {
    }

    /// <param name="repositoryPath">The repository the submodule is added to.</param>
    /// <param name="runGitWithOutput">Runs the add command with its output shown (upstream's FormProcess).</param>
    public AddSubmoduleWindow(string repositoryPath, Func<string, Task<bool>> runGitWithOutput)
    {
        ViewModel = new AddSubmoduleViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath),
            AppServices.RemoteUrls, runGitWithOutput) { PickFolder = PickFolderAsync };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, AddSubmoduleViewModel.Category);
        InitialFocus.Attach(this, () => Directory);
        Branch.DropDownOpened += (_, _) => BranchesLoaded = LoadBranchesAsync();
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public AddSubmoduleViewModel ViewModel { get; }

    /// <summary>
    ///  The last read of the remote's branches, for tests.
    /// </summary>
    public Task BranchesLoaded { get; private set; } = Task.CompletedTask;

    // View concern: the editable list clears its text when its items change, so the typed branch is put back.
    private async Task LoadBranchesAsync()
    {
        string? text = Branch.Text;
        await ViewModel.LoadBranchesAsync();
        Branch.Text = text;
    }

    private async Task<string?> PickFolderAsync(string? startPath)
    {
        IStorageFolder? start = startPath is null ? null : await StorageProvider.TryGetFolderFromPathAsync(startPath);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
