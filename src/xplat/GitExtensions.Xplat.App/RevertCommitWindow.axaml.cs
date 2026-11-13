using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRevertCommit</c>, bound to <see cref="RevertCommitViewModel"/> (rules in
///  <see cref="RevertCommitDialog"/>): the commit to revert, the parent of a merge, and whether to commit. Revert runs the
///  caller's revert (upstream's FormProcess and conflict handling) and closes with true.
/// </summary>
public partial class RevertCommitWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public RevertCommitWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The commit to revert.</param>
    /// <param name="revert">
    ///  Runs the revert command with its output shown and handles its conflicts as upstream's
    ///  <c>MergeConflictHandler</c> (the window, the command, and whether a commit is offered after the conflicts).
    /// </param>
    public RevertCommitWindow(string repositoryPath, string revision, Func<Window, ArgumentString, bool, Task> revert)
    {
        ViewModel = new RevertCommitViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), revision,
            (arguments, autoCommit) => revert(this, arguments, autoCommit));
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, RevertCommitViewModel.Category);
        InitialFocus.Attach(this, () => lvParentsList.IsVisible ? lvParentsList : AutoCommit);
        lvParentsList.ItemTemplate =
            new FuncDataTemplate<CherryPickParent>((parent, _) => CherryPickWindow.ParentRow(parent));
        Opened += (_, _) => UiActions.Run(ViewModel.LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public RevertCommitViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the commit and its parents are shown, for tests.
    /// </summary>
    public Task RevisionLoaded => ViewModel.Loaded;
}
