using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCompareToBranch</c> with its <c>BranchSelector</c>, bound to <see cref="CompareToBranchViewModel"/>:
///  a local or remote branch to compare a commit with, showing how many commits each has that the other lacks. Closes with
///  the branch name, or null when closed without one.
/// </summary>
public partial class CompareToBranchWindow : Window
{
    public CompareToBranchWindow()
        : this("", default)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="commitToCompare">The commit the branches' commits are counted against; HEAD when zero.</param>
    public CompareToBranchWindow(string repositoryPath, ObjectId commitToCompare)
    {
        ViewModel = new CompareToBranchViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), commitToCompare);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, CompareToBranchViewModel.Category);
        InitialFocus.Attach(this, () => Branches);

        // View concern: without a branch, Compare keeps the focus on the list.
        ViewModel.BranchRequired += (_, _) => Branches.Focus();
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public CompareToBranchViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the first branch list is shown, for tests.
    /// </summary>
    public Task BranchesLoaded => ViewModel.Loaded;
}
