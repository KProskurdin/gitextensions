using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtUtils.GitUI.Theming;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormReflog</c> in the new shell: the reflog of HEAD or a branch (reading in <see cref="Reflog"/>), with
///  upstream's menu to copy an entry's hash, create a branch on it or reset the current branch to it.
/// </summary>
public partial class ReflogWindow : Window
{
    // Upstream's texts.
    public const string CurrentBranchFormat = "current branch ({0})";

    public const string ContinueResetWithChanges =
        "You have changes in your working directory that could be lost.\n\nDo you want to continue?";

    public const string ContinueResetCaption = "Changes not committed...";

    private readonly string _repositoryPath;
    private readonly Func<Window, ObjectId, Task> _createBranch;
    private readonly Func<Window, ObjectId, ResetMode, Task> _resetCurrentBranch;
    private readonly GitModule _module;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _currentBranch;
    private bool _isDirtyDir;
    private Task _entriesLoaded = Task.CompletedTask;

    /// <param name="createBranch">Opens the create branch window on a commit, as upstream's reflog opens it.</param>
    /// <param name="resetCurrentBranch">Opens the reset current branch window on a commit with a reset type.</param>
    public ReflogWindow(string repositoryPath, Func<Window, ObjectId, Task> createBranch,
        Func<Window, ObjectId, ResetMode, Task> resetCurrentBranch)
    {
        _repositoryPath = repositoryPath;
        _createBranch = createBranch;
        _resetCurrentBranch = resetCurrentBranch;
        _module = GitModules.Open(repositoryPath);
        InitializeComponent();
        InitialFocus.Attach(this);
        DirtyWarningText.Foreground = new SolidColorBrush(ToAvalonia(System.Drawing.Color.Red.AdaptForeColor(
            ThemeBrushes.Current.IsDark
                ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
                : System.Drawing.Color.White)));
        ReferencesBox.SelectionChanged += (_, _) => Run(() => _entriesLoaded = ShowReflogAsync());
        CurrentBranchLink.Click += (_, _) => ReferencesBox.SelectedItem = _currentBranch;
        HeadLink.Click += (_, _) => ReferencesBox.SelectedIndex = 0;
        CopyShaMenuItem.Click += (_, _) => Run(CopyShaAsync);
        CreateBranchMenuItem.Click += (_, _) => Run(CreateBranchAsync);
        ResetBranchMenuItem.Click += (_, _) => Run(ResetCurrentBranchAsync);
        EntryMenu.Opening += (_, e) => e.Cancel = EntryList.SelectedItem is null;

        // Upstream selects the row under the mouse, and a click opens the menu on it.
        EntryList.PointerMoved += (_, e) =>
        {
            if ((e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is
                { DataContext: ReflogEntry entry })
            {
                EntryList.SelectedItem = entry;
            }
        };
        EntryList.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Left && EntryList.SelectedItem is not null)
            {
                EntryMenu.Open(EntryList);
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the references are listed and the first reflog is shown, for tests.
    /// </summary>
    public Task ReflogLoaded => _loaded.Task;

    /// <summary>
    ///  The last read of a reflog, for tests.
    /// </summary>
    public Task EntriesLoaded => _entriesLoaded;

    // Upstream's FormReflog_Load: the dirty warning, the current branch link (on a branch only) and the references.
    private async Task LoadAsync()
    {
        (bool dirty, string branch, IReadOnlyList<string> references) = await Task.Run(() =>
            (_module.IsDirtyDir(), _module.GetSelectedBranch(), Reflog.References(_module)));
        _isDirtyDir = dirty;
        _currentBranch = branch;
        bool onBranch = branch != DetachedHeadParser.DetachedBranch;
        CurrentBranchLink.Content = string.Format(CurrentBranchFormat, branch);
        CurrentBranchLink.IsVisible = onBranch;
        DirtyWarningText.IsVisible = dirty;
        ResetBranchMenuItem.IsEnabled = onBranch;
        ReferencesBox.ItemsSource = references;
        ReferencesBox.SelectedIndex = 0;
        await _entriesLoaded;
        _loaded.TrySetResult();
    }

    // Upstream's Branches_SelectedIndexChanged.
    private async Task ShowReflogAsync()
    {
        if (ReferencesBox.SelectedItem is not string reference)
        {
            return;
        }

        IReadOnlyList<ReflogEntry> entries = await Reflog.LoadAsync(_repositoryPath, reference);
        if (Equals(ReferencesBox.SelectedItem, reference))
        {
            EntryList.ItemsSource = entries;
        }
    }

    private async Task CopyShaAsync()
    {
        if (EntryList.SelectedItem is ReflogEntry entry && GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(entry.Sha.ToString());
        }
    }

    // Upstream's createABranchOnThisCommitToolStripMenuItem_Click: the create branch window on the entry's commit, which
    // cannot be changed, without checkout or orphan.
    private async Task CreateBranchAsync()
    {
        if (EntryList.SelectedItem is ReflogEntry entry)
        {
            await _createBranch(this, entry.Sha);
        }
    }

    // Upstream's resetCurrentBranchOnThisCommitToolStripMenuItem_Click: with local changes it asks first and proposes a
    // soft reset, otherwise a hard one.
    private async Task ResetCurrentBranchAsync()
    {
        if (EntryList.SelectedItem is not ReflogEntry entry)
        {
            return;
        }

        if (_isDirtyDir)
        {
            MessageBoxWindow question = new(Ui.UpstreamTranslation.ByText(ContinueResetWithChanges),
                Ui.UpstreamTranslation.ByText(ContinueResetCaption), MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxDefaultButton.Button2);
            await question.ShowDialog(this);
            if (question.Result != DialogResult.Yes)
            {
                return;
            }
        }

        await _resetCurrentBranch(this, entry.Sha, _isDirtyDir ? ResetMode.Soft : ResetMode.Hard);
    }

    private static Color ToAvalonia(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
