using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormClone</c> in the new shell, bound to <see cref="CloneViewModel"/> (rules in
///  <see cref="CloneDialog"/>): clones a URL or local repository into a new subdirectory of the destination, with the
///  branch to check out, the repository type and the options. Closes with true after cloning.
/// </summary>
public partial class CloneWindow : Window
{
    // Upstream's BrowseForPrivateKey texts.
    private const string SshKeyFilter = " (*.ppk)";
    private const string SshKeyPattern = "*.ppk";

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CloneWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryPath">The open repository, whose remote and folder the dialog proposes; null for none.</param>
    /// <param name="recentRepositories">The recent local repositories, whose parent folders are offered as destinations.</param>
    /// <param name="addRecent">Adds the clone to the recent repositories.</param>
    /// <param name="openRepository">Opens the clone, when the user wants to.</param>
    /// <param name="url">The URL (or destination folder) to start with; null for none.</param>
    public CloneWindow(string? repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        IReadOnlyList<string> recentRepositories, Func<string, Task> addRecent, Func<string, Task> openRepository,
        string? url = null)
    {
        ViewModel = new CloneViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath ?? ""), actions,
            preferences, AppServices.RemoteUrls, recentRepositories, addRecent, openRepository, url)
        {
            ReadClipboard = ClipboardTextAsync,
            PickFolder = PickFolderAsync,
            PickSshKey = PickSshKeyAsync,
            GiveKeyToPageant = path => Putty.StartPageantIfConfigured(path, UpstreamTranslation.ByText),
        };
        DataContext = ViewModel;
        InitializeComponent();
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("getting_started", "clone-repository"));

        // View concerns: Load SSH key only with PuTTY, the remote's branches read when the list opens, the focus, and
        // Escape closing the window.
        LoadSshKeyButton.IsVisible = GitSshHelpers.IsPlink;
        BranchesBox.DropDownOpened += (_, _) => ViewModel.LoadBranches();
        ViewModel.DestinationRequired += (_, _) => ToBox.Focus();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => UiActions.Run(async () =>
        {
            await ViewModel.LoadAsync();
            FromBox.Focus();
        }, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        Closed += (_, _) => ViewModel.Cancel();
    }

    public CloneViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public CloneDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the histories, the source and the destination are shown, for tests.
    /// </summary>
    public Task CloneLoaded => ViewModel.Loaded;

    /// <summary>
    ///  The last read of the remote's branches, for tests.
    /// </summary>
    public Task BranchesLoaded => ViewModel.BranchesLoaded;

    /// <summary>
    ///  Gives a key to Pageant, returning the error if any. Tests replace it, so that none starts the user's Pageant.
    /// </summary>
    public Func<string, string?> GiveKeyToPageant
    {
        get => ViewModel.GiveKeyToPageant;
        set => ViewModel.GiveKeyToPageant = value;
    }

    /// <summary>
    ///  Upstream's <c>BrowseAndLoad</c>: the key is given to Pageant, and used only when that worked.
    /// </summary>
    public Task UseSshKeyAsync(string path) => ViewModel.UseSshKeyAsync(path);

    // Upstream tries the clipboard to be more helpful, and ignores a clipboard it cannot read.
    private async Task<string?> ClipboardTextAsync()
    {
        try
        {
            return Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private async Task<string?> PickFolderAsync(string? startPath)
    {
        IStorageFolder? start = string.IsNullOrWhiteSpace(startPath) || !Directory.Exists(startPath)
            ? null
            : await StorageProvider.TryGetFolderFromPathAsync(startPath);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = start });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private async Task<string?> PickSshKeyAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = CloneViewModel.SshKeyTitle,
            FileTypeFilter = [new FilePickerFileType(SshKeyFilter) { Patterns = [SshKeyPattern] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
