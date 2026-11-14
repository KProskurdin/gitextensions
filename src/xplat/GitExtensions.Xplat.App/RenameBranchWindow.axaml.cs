using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRenameBranch</c>, bound to <see cref="RenameBranchViewModel"/>: a new name for a local branch,
///  normalised as Settings > Advanced says. Closes with true when git renamed the branch.
/// </summary>
public partial class RenameBranchWindow : Window
{
    public RenameBranchWindow()
        : this("", AppServices.Preferences, _ => Task.FromResult(false))
    {
    }

    /// <param name="branch">The branch to rename, also the box's first text.</param>
    /// <param name="preferences">Where "Auto normalise branch name" and its symbol are kept.</param>
    /// <param name="runGitWithOutput">Runs the rename with its output shown (upstream's FormProcess).</param>
    public RenameBranchWindow(string branch, IAppPreferences preferences, Func<string, Task<bool>> runGitWithOutput)
    {
        ViewModel = new RenameBranchViewModel(new WindowDialogHost(this), branch, preferences, runGitWithOutput);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, RenameBranchViewModel.Category);
        InitialFocus.Attach(this, () => BranchNameTextBox);

        // View concern: the name is normalised when its box is left, keeping the caret where it was.
        BranchNameTextBox.LostFocus += (_, _) =>
        {
            int caret = BranchNameTextBox.CaretIndex;
            ViewModel.NormaliseName();
            BranchNameTextBox.CaretIndex = Math.Min(caret, ViewModel.BranchName.Length);
        };
    }

    public RenameBranchViewModel ViewModel { get; }
}
