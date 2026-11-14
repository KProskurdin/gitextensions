using Avalonia.Controls;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormFindInCommitFilesGitGrep</c>, bound to <see cref="FindInCommitFilesGitGrepViewModel"/>: a window
///  beside a file list that searches the list's commit with git-grep. When the window closes with no expression, or with
///  the list's box hidden, the search is cleared.
/// </summary>
public partial class FindInCommitFilesGitGrepWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public FindInCommitFilesGitGrepWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="locate">Searches the list with an expression ("" clears the search).</param>
    /// <param name="toggleBox">Shows or hides the list's git-grep box.</param>
    public FindInCommitFilesGitGrepWindow(IAppPreferences preferences, Action<string> locate, Action<bool> toggleBox)
    {
        ViewModel = new FindInCommitFilesGitGrepViewModel(new WindowDialogHost(this), preferences, locate, toggleBox);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, FindInCommitFilesGitGrepViewModel.Category);
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("browse_repository", "diff"));
        Closing += (_, _) => ViewModel.OnClosing();
        Opened += (_, _) =>
        {
            ViewModel.Load();
            cboFindInCommitFilesGitGrep.Focus();
        };
    }

    public FindInCommitFilesGitGrepViewModel ViewModel { get; }

    /// <summary>
    ///  The git-grep expression.
    /// </summary>
    public string GitGrepExpressionText
    {
        get => ViewModel.Expression ?? "";
        set => ViewModel.Expression = value;
    }

    /// <summary>
    ///  Upstream's <c>SetSearchItems</c>: the list's earlier searches in the drop-down, the expression kept.
    /// </summary>
    public void SetSearchItems(IEnumerable<string> items) => ViewModel.SetSearchItems(items);

    /// <summary>
    ///  Upstream's <c>SetShowFindInCommitFilesGitGrep</c>: the check box follows the list's box.
    /// </summary>
    public void SetShowFindInCommitFilesGitGrep(bool visible) => ViewModel.SetShowSearchBox(visible);
}
