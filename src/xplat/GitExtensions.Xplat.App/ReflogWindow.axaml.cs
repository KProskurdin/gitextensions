using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormReflog</c> in the new shell, bound to <see cref="ReflogViewModel"/>: the reflog of HEAD or a branch,
///  with upstream's menu to copy an entry's hash, create a branch on it or reset the current branch to it.
/// </summary>
public partial class ReflogWindow : Window
{
    // Upstream's texts.
    public const string CurrentBranchFormat = ReflogViewModel.CurrentBranchFormat;
    public const string ContinueResetWithChanges = ReflogViewModel.ContinueResetWithChanges;
    public const string ContinueResetCaption = ReflogViewModel.ContinueResetCaption;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ReflogWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="createBranch">Opens the create branch window on a commit, as upstream's reflog opens it.</param>
    /// <param name="resetCurrentBranch">Opens the reset current branch window on a commit with a reset type.</param>
    public ReflogWindow(string repositoryPath, Func<Window, ObjectId, Task> createBranch,
        Func<Window, ObjectId, ResetMode, Task> resetCurrentBranch)
    {
        ViewModel = new ReflogViewModel(new WindowDialogHost(this), repositoryPath, GitModules.Open(repositoryPath),
            id => createBranch(this, id), (id, mode) => resetCurrentBranch(this, id, mode))
        {
            CopyText = text => Clipboard?.SetTextAsync(text) ?? Task.CompletedTask,
        };
        DataContext = ViewModel;
        InitializeComponent();
        InitialFocus.Attach(this);

        // A context menu gets its data context when it opens; its commands are bound before, as tests and hotkeys press
        // its items.
        EntryMenu.DataContext = ViewModel;

        // View concerns: upstream's red warning in the theme's colors, and its menu, which opens on the row under the
        // mouse with a click and not without a row.
        DirtyWarningText.Foreground = new SolidColorBrush(ToAvalonia(System.Drawing.Color.Red.AdaptForeColor(
            ThemeBrushes.Current.IsDark
                ? System.Drawing.Color.FromArgb(0x20, 0x20, 0x20)
                : System.Drawing.Color.White)));
        EntryMenu.Opening += (_, e) => e.Cancel = EntryList.SelectedItem is null;
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
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public ReflogViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the references are listed and the first reflog is shown, for tests.
    /// </summary>
    public Task ReflogLoaded => ViewModel.Loaded;

    /// <summary>
    ///  The last read of a reflog, for tests.
    /// </summary>
    public Task EntriesLoaded => ViewModel.EntriesLoaded;

    private static Color ToAvalonia(System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);
}
