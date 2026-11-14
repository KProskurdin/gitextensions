using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using TaskDialogButton = System.Windows.Forms.TaskDialogButton;
using TaskDialogCommandLinkButton = System.Windows.Forms.TaskDialogCommandLinkButton;
using TaskDialogPage = System.Windows.Forms.TaskDialogPage;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The repository's git questions the pull window asks before it closes: a branch's <c>branch.&lt;name&gt;.remote</c>, and
///  whether a rebase would rebase a merge commit (remote, remote branch, current branch). <see cref="ManageRemotes"/>
///  opens the remotes window over the pull window with a remote chosen (or none) and lists the remotes again.
///  <see cref="CheckoutBranch"/> is upstream's <c>StartCheckoutBranch</c> over the pull window: true when a branch was
///  checked out.
/// </summary>
public sealed record PullWindowGit(
    Func<string, Task<string?>> BranchRemoteOf,
    Func<string, string, string, Task<bool>> RebasesMergeCommit,
    Func<Window, string?, Task<IReadOnlyList<string>>>? ManageRemotes = null,
    Func<Window, Task<bool>>? CheckoutBranch = null);

/// <summary>
///  Upstream's <c>FormPull</c> in the new shell, bound to <see cref="PullViewModel"/> (rules in
///  <see cref="PullDialog"/>): where to pull from, the branches, merge, rebase or fetch, the tags and prune options.
///  Closes with the request to run, or null when closed without pulling.
/// </summary>
public partial class PullWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public PullWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="remoteBranches">The remote-tracking branches ("origin/main") the remote branch box offers.</param>
    /// <param name="localBranches">The local branches it offers when pulling from a URL.</param>
    /// <param name="isShallow">True when the repository is a shallow clone, which offers the full history.</param>
    /// <param name="solveConflicts">Opens the conflicts window (upstream's "Solve conflicts").</param>
    /// <param name="manageStashes">Opens the stash window over this one (upstream's "Stash changes").</param>
    public PullWindow(PullDialog dialog, IAppPreferences preferences, string displayPath,
        IReadOnlyList<string> remoteBranches, IReadOnlyList<string> localBranches, bool isShallow, PullWindowGit git,
        Action? solveConflicts = null, Func<Window, Task>? manageStashes = null)
    {
        ViewModel = new PullViewModel(new WindowDialogHost(this), dialog, preferences, AppServices.RemoteUrls,
            displayPath, remoteBranches, localBranches, isShallow, new PullViewModelGit(
                git.BranchRemoteOf,
                git.RebasesMergeCommit,
                () => PullWhileDetachedAsync(git.CheckoutBranch),
                UseLocalBranchNameAsync,
                git.ManageRemotes is null ? null : remote => git.ManageRemotes(this, remote),
                solveConflicts,
                manageStashes is null ? null : () => manageStashes(this),
                PickFolderAsync));
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this, () => RemotesBox);

        // View concerns: the help picture, the remote branches listed when the list opens, and Escape closing the window.
        HelpImage.Load(preferences, "Pull");
        HelpImage.NoticeText = UpstreamTranslation.ByText(PullDialog.HoverShowImageText);
        ShowHelpImages();
        ViewModel.PropertyChanged += OnViewModelChanged;
        RemoteBranchBox.DropDownOpened += (_, _) => ViewModel.ShowRemoteBranches();
        Opened += (_, _) => _ = ViewModel.LoadUrlHistoryAsync();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(null);
            }
        };
    }

    public PullViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public PullDialog Dialog => ViewModel.Dialog;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PullViewModel.HelpImages))
        {
            ShowHelpImages();
        }
    }

    private void ShowHelpImages()
    {
        (string image, string? hoverImage) = ViewModel.HelpImages;
        HelpImage.Image1 = UpstreamImages.Named(image);
        HelpImage.Image2 = hoverImage is null ? null : UpstreamImages.Named(hoverImage);
        HelpImage.IsOnHoverShowImage2 = hoverImage is not null;
    }

    // Upstream's detached HEAD question, a task dialog: check out a branch first (the pull then goes on), go on, or cancel.
    private async Task<bool> PullWhileDetachedAsync(Func<Window, Task<bool>>? checkoutBranch)
    {
        TaskDialogCommandLinkButton checkoutButton = new(UpstreamTranslation.ByText(PullDialog.CheckoutBranchText));
        TaskDialogCommandLinkButton continueButton = new(UpstreamTranslation.ByText(PullDialog.ContinueText));
        MessageBoxWindow question = new(new TaskDialogPage
        {
            Text = UpstreamTranslation.ByText(PullDialog.NotOnBranch),
            Heading = UpstreamTranslation.ByText(PullDialog.NotOnBranchHeading),
            Caption = UpstreamTranslation.ByText(PullDialog.NotOnBranchCaption),
            Buttons = { TaskDialogButton.Cancel, checkoutButton, continueButton },
            AllowCancel = true,
        });
        await question.ShowDialog(this);
        return question.ChosenButton == continueButton
            || (question.ChosenButton == checkoutButton && checkoutBranch is not null && await checkoutBranch(this));
    }

    // Upstream's question for a missing remote branch, a task dialog.
    private async Task<bool> UseLocalBranchNameAsync(string text, string button)
    {
        TaskDialogButton useLocalBranch = new(button);
        MessageBoxWindow question = new(new TaskDialogPage
        {
            Text = text,
            Caption = UpstreamTranslation.ByText(PullDialog.NoRemoteBranchCaption),
            Heading = UpstreamTranslation.ByText(PullDialog.NoRemoteBranch),
            Buttons = { TaskDialogButton.Cancel, useLocalBranch },
        });
        await question.ShowDialog(this);
        return question.ChosenButton == useLocalBranch;
    }

    private async Task<string?> PickFolderAsync()
    {
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
