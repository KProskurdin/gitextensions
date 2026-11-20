using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormInit</c>, bound to <see cref="InitRepositoryViewModel"/>: a new repository in a folder, personal or
///  central (bare and shared), made by upstream's <c>GitModule.Init</c>, whose answer it shows.
/// </summary>
public partial class InitRepositoryWindow : Window
{
    public InitRepositoryWindow()
        : this("", [])
    {
    }

    /// <param name="directory">The folder at first; upstream's default clone destination when empty.</param>
    /// <param name="recentDirectories">The recent repositories, which the folder's list offers.</param>
    public InitRepositoryWindow(string directory, IReadOnlyList<string> recentDirectories)
    {
        ViewModel = new InitRepositoryViewModel(new WindowDialogHost(this),
            string.IsNullOrEmpty(directory) ? AppSettings.DefaultCloneDestinationPath : directory, recentDirectories,
            (folder, central) => Task.Run(() =>
            {
                // As upstream: the folder is made when it does not exist.
                Directory.CreateDirectory(folder);
                return GitModules.Open(folder).Init(bare: central, shared: central);
            }))
        {
            PickFolder = PickFolderAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, InitRepositoryViewModel.Category);
        InitialFocus.Attach(this, () => DirectoryBox);

        // Upstream's ManualSectionSubfolder and ManualSectionAnchorName.
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("getting_started", "create-new-repository"));
    }

    public InitRepositoryViewModel ViewModel { get; }

    private async Task<string?> PickFolderAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
