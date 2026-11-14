using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormPush</c> in the new shell, bound to <see cref="PushViewModel"/> (rules in
///  <see cref="PushDialog"/>): where to push (a remote or a URL), and a branch, a tag, or several branches. Closes with
///  the push to run, or null.
/// </summary>
public partial class PushWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public PushWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="canCreatePullRequest">True when a repository host plugin knows the repository; a remote on Azure DevOps
    ///  enables the choice too, as upstream's.</param>
    /// <param name="openPull">Opens the pull dialog over this window (upstream's Pull button).</param>
    /// <param name="manageRemotes">Opens the remotes window over this window with a remote chosen, and reads the
    ///  repository again (upstream's Manage remotes).</param>
    public PushWindow(PushDialog dialog, IAppPreferences preferences, string workingDirectory,
        bool canCreatePullRequest = false, Func<Window, Task>? openPull = null,
        Func<Window, string?, Task<PushRepository>>? manageRemotes = null, Func<string, Task>? loadSshKey = null,
        Func<string, Task<IReadOnlyList<IGitRef>?>>? readRemoteHeads = null)
    {
        ViewModel = new PushViewModel(new WindowDialogHost(this), dialog, preferences, AppServices.RemoteUrls,
            workingDirectory, canCreatePullRequest,
            openPull is null ? null : () => openPull(this),
            manageRemotes is null ? null : remote => manageRemotes(this, remote),
            loadSshKey, readRemoteHeads,
            () => ConfirmWindow.AskAsync(this, preferences, Confirmation.PushNewBranch,
                UpstreamTranslation.ByText(Confirmations.PushNewBranchQuestion), UpstreamTranslation.ByText("Push"),
                UpstreamTranslation.ByText(PushDialog.PushCaption)))
        {
            PickDestination = PickDestinationAsync,
        };
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this, () => PushButton);

        // View concerns: the URL history loads once the window shows, and Escape closes it.
        Opened += (_, _) => _ = ViewModel.LoadUrlHistoryAsync();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        };
    }

    public PushViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public PushDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Upstream's "Create pull request after push", read by the caller once the push succeeded.
    /// </summary>
    public bool CreatePullRequestAfterPush => ViewModel.Dialog.CreatePullRequest;

    // Upstream's folder browser: a local repository to push to, as a file URL.
    private async Task<string?> PickDestinationAsync()
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 && folders[0].TryGetLocalPath() is { } path ? new Uri(path).AbsoluteUri : null;
    }
}
