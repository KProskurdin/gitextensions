using System.ComponentModel;
using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormMergeBranch</c> in the new shell, bound to <see cref="MergeBranchViewModel"/> (rules in
///  <see cref="MergeBranchDialog"/>): merges a branch, remote branch or tag into the current branch, fast-forward or with a
///  merge commit, optionally without committing, with the advanced options. Closes with true after merging (or stopping on
///  conflicts).
/// </summary>
public partial class MergeBranchWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public MergeBranchWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="defaultBranch">The branch to merge; null for the branch the current one tracks.</param>
    /// <param name="withScripts">Runs the merge between the before and after merge scripts.</param>
    /// <param name="showConflicts">Opens the conflicts window.</param>
    public MergeBranchWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        string? defaultBranch, Func<string, Func<Task<bool>>, Task<bool>> withScripts, Action showConflicts)
    {
        ViewModel = new MergeBranchViewModel(new WindowDialogHost(this), repositoryPath,
            GitModules.Open(repositoryPath),
            actions, preferences, AppServices.MergeSettings, defaultBranch, withScripts, showConflicts,
            () => ConfirmWindow.AskAsync(this, preferences, Confirmation.ResolveConflicts,
                UpstreamTranslation.ByText(Confirmations.ResolveConflictsQuestion), "Yes",
                UpstreamTranslation.ByText(Confirmations.ResolveConflictsCaption)));
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: upstream's help picture, which shows the fast forward case while fast forward is chosen; the
        // strategy help in the browser; the branch list's focus once read.
        HelpImage.Load(preferences, "MergeBranches");
        HelpImage.NoticeText = UpstreamTranslation.ByText(MergeBranchDialog.HoverShowImageText);
        HelpImage.Image1 = UpstreamImages.Named("HelpCommandMerge");
        HelpImage.Image2 = UpstreamImages.Named("HelpCommandMergeFastForward");
        HelpImage.IsOnHoverShowImage2 = true;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MergeBranchViewModel.Dialog))
            {
                ViewModel.Dialog.PropertyChanged += OnDialogChanged;
                ShowHelpImage();
            }
        };
        ViewModel.Dialog.PropertyChanged += OnDialogChanged;
        StrategyHelpLink.Click += (_, _) =>
            OsShellUtil.OpenUrlInDefaultBrowser(
                GitUI.UserManual.UserManual.UrlFor("branches", "advanced-merge-options"));
        Opened += (_, _) => _ = ShowAsync();
    }

    public MergeBranchViewModel ViewModel { get; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public MergeBranchDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the branches and settings are read, for tests.
    /// </summary>
    public Task MergeLoaded => ViewModel.Loaded;

    private async Task ShowAsync()
    {
        await ViewModel.LoadAsync();
        BranchesBox.Focus();
    }

    private void OnDialogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == ViewModel.Dialog && e.PropertyName == nameof(MergeBranchDialog.NoFastForward))
        {
            ShowHelpImage();
        }
    }

    private void ShowHelpImage() => HelpImage.IsOnHoverShowImage2 = !ViewModel.Dialog.NoFastForward;
}
