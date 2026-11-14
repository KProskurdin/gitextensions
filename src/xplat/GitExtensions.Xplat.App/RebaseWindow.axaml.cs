using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRebase</c> in the new shell, bound to <see cref="RebaseViewModel"/> (rules in
///  <see cref="RebaseDialog"/>, the commit list in <see cref="RebasePatches"/>): rebases the current branch on a branch,
///  tag or commit with upstream's options and specific range, and while a rebase is in progress shows its commits and
///  offers Commit, Edit todo, Skip, Solve conflicts, Continue and Abort. Closes with true when the rebase is done or
///  aborted.
/// </summary>
public partial class RebaseWindow : Window
{
    private const string HelpDialogId = "Rebase";
    private const string HelpImageName = "HelpCommandRebase";

    // Upstream's OtherColors.MergeConflictsColor, its MergeToolPanel colors (yellow, or 136,136,0 in dark mode) and the
    // OrangeRed of the commit being applied.
    private static readonly IBrush _conflictsBrush = new SolidColorBrush(Color.FromRgb(230, 99, 99));

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public RebaseWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="from">Upstream's specific range start (excluded); empty for none.</param>
    /// <param name="to">The branch the specific range ends at; null for the current one.</param>
    /// <param name="onto">The branch or commit to rebase on; null for none.</param>
    /// <param name="interactive">Upstream's "Interactive Rebase" to start with.</param>
    /// <param name="startRebaseImmediately">Upstream's: rebase at once, as the grid's "Selected commit" does.</param>
    /// <param name="showCommit">Opens the commit window over this one.</param>
    /// <param name="showConflicts">Opens the conflicts window over this one.</param>
    public RebaseWindow(string repositoryPath, RepositoryOperationsViewModel actions, IAppPreferences preferences,
        string? from, string? to, string? onto, bool interactive, bool startRebaseImmediately, Action<Window> showCommit,
        Action<Window> showConflicts)
    {
        ViewModel = new RebaseViewModel(new WindowDialogHost(this), repositoryPath, GitModules.Open(repositoryPath),
            actions, preferences, from, to, onto, interactive, startRebaseImmediately)
        {
            ChooseCommit = async (preselect, mergeBase) =>
            {
                ChooseCommitWindow choose = new(repositoryPath, preselect, preferences, showCurrentBranchOnly: true,
                    lastRevisionToDisplayHash: mergeBase);
                return (await choose.ShowDialog<CommitRow?>(this))?.Hash;
            },
            ShowAddFiles = () => new AddFilesWindow(null, RunGit).ShowDialog(this),
            ShowCommit = () => showCommit(this),
            ShowConflicts = () => showConflicts(this),
        };
        DataContext = ViewModel;
        InitializeComponent();

        // View concerns: the help picture, the conflict colors, the default button's highlight and focus, the commits in
        // their colors, a double click opening one, and Escape closing the window.
        HelpImage.Load(preferences, HelpDialogId);
        HelpImage.NoticeText = UpstreamTranslation.ByText(RebaseDialog.HoverShowImageText);
        HelpImage.Image1 = UpstreamImages.Named(HelpImageName);
        UnresolvedConflictsButton.Background = _conflictsBrush;
        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.BranchesShown += (_, _) => BranchesBox.Focus();
        PatchesList.DoubleTapped += (_, _) => UiActions.Run(
            () => RebasePatchRow.OpenAsync(this, repositoryPath, PatchesList.SelectedItem as RebasePatchRow),
            ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close(false);
            }
        };
        Opened += (_, _) => _ = ViewModel.LoadAsync();

        // Upstream refreshes after its modal commit and conflicts dialogs; here the windows are not modal, so the state is
        // read again when this window is active again.
        Activated += (_, _) => ViewModel.Refresh();
    }

    public RebaseViewModel ViewModel { get; }

    /// <summary>
    ///  Runs git with the arguments, showing its output (upstream's FormProcess), for "Add files"; true when git
    ///  succeeded. Without it the button does nothing.
    /// </summary>
    public Func<string, Task<bool>>? RunGit { get; init; }

    /// <summary>
    ///  The dialog state, for tests.
    /// </summary>
    public RebaseDialog Dialog => ViewModel.Dialog;

    /// <summary>
    ///  Completes when the branches, the options and the rebase state are shown, for tests.
    /// </summary>
    public Task RebaseLoaded => ViewModel.Loaded;

    /// <summary>
    ///  The rows of the commit list, for tests.
    /// </summary>
    public IReadOnlyList<RebasePatch> Patches => ViewModel.Patches;

    private static Color ToAvalonia(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(RebaseViewModel.Buttons):
                ShowDefaultButton(ViewModel.Buttons.Default);
                break;
            case nameof(RebaseViewModel.Conflicted):
                MergeToolPanel.Background = ViewModel.Conflicted
                    ? new SolidColorBrush(ThemeBrushes.Current.IsDark
                        ? Color.FromRgb(136, 136, 0)
                        : ToAvalonia(System.Drawing.Color.Yellow.AdaptBackColor()))
                    : Brushes.Transparent;
                break;
            case nameof(RebaseViewModel.Patches):
                ShowPatches(ViewModel.Patches);
                break;
        }
    }

    // Upstream's AcceptButton, which takes the focus.
    private void ShowDefaultButton(RebaseDefaultButton button)
    {
        RebaseButton.IsDefault = button == RebaseDefaultButton.Rebase;
        ContinueButton.IsDefault = button == RebaseDefaultButton.Continue;
        SolveConflictsButton.IsDefault = button == RebaseDefaultButton.SolveConflicts;
        if (button == RebaseDefaultButton.Continue)
        {
            ContinueButton.Focus();
        }
        else if (button == RebaseDefaultButton.SolveConflicts)
        {
            SolveConflictsButton.Focus();
        }
    }

    // Upstream's DisplayPatches: the commit being applied selected, in OrangeRed.
    private void ShowPatches(IReadOnlyList<RebasePatch> patches)
    {
        IBrush applying = new SolidColorBrush(ToAvalonia(System.Drawing.Color.OrangeRed.AdaptForeColor(
            ThemeBrushes.Current.IsDark ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20) : System.Drawing.Color.White)));
        List<RebasePatchRow> rows = [.. patches.Select(patch => new RebasePatchRow(patch, patch.IsNext ? applying : null))];
        PatchesList.ItemsSource = rows;
        PatchesList.SelectedItem = rows.FirstOrDefault(row => row.Patch.IsNext);
        if (PatchesList.SelectedItem is { } current)
        {
            PatchesList.ScrollIntoView(current);
        }
    }
}

/// <summary>
///  A row of the rebase window's commit list: the commit, its subject on one line, and the color of the one being applied.
/// </summary>
public sealed record RebasePatchRow(RebasePatch Patch, IBrush? Foreground)
{
    // Upstream's PatchGrid text.
    private const string UnableToShowPatchDetails = "Unable to show details of patch file.";
    private const string ErrorCaption = "Error";

    public string Subject => (Patch.Subject ?? "").ReplaceLineEndings(" ").Trim();

    /// <summary>
    ///  Upstream's <c>PatchGrid.Patches_DoubleClick</c>: a commit opens its diff, a patch file the view patch window, and a
    ///  row with neither upstream's error.
    /// </summary>
    public static async Task OpenAsync(Window owner, string repositoryPath, RebasePatchRow? row)
    {
        if (row?.Patch is not { } patch)
        {
            return;
        }

        if (patch.ObjectId is { IsZero: false, IsArtificial: false } objectId)
        {
            new CommitDiffWindow(repositoryPath, objectId.ToString()).Show(owner);
        }
        else if (string.IsNullOrEmpty(patch.FullName))
        {
            await new MessageBoxWindow(UpstreamTranslation.ByText(UnableToShowPatchDetails),
                UpstreamTranslation.ByText(ErrorCaption), MessageBoxButtons.OK).ShowDialog(owner);
        }
        else
        {
            await new ViewPatchWindow(repositoryPath, patch.FullName).ShowDialog(owner);
        }
    }
}
