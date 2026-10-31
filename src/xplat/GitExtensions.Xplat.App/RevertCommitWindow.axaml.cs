using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitUIPluginInterfaces;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormRevertCommit</c> (rules in <see cref="RevertCommitDialog"/>): the commit to revert, the parent of a
///  merge, and whether to commit. Revert runs the caller's revert (upstream's FormProcess and conflict handling) and closes
///  with true.
/// </summary>
public partial class RevertCommitWindow : Window
{
    private readonly GitModule _module;
    private readonly string _revision;
    private readonly Func<Window, ArgumentString, bool, Task> _revert;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GitRevision? _selected;
    private bool _isMerge;

    public RevertCommitWindow()
        : this("", "HEAD", (_, _, _) => Task.CompletedTask)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="revision">The commit to revert.</param>
    /// <param name="revert">
    ///  Runs the revert command with its output shown and handles its conflicts as upstream's
    ///  <c>MergeConflictHandler</c> (the window, the command, and whether a commit is offered after the conflicts).
    /// </param>
    public RevertCommitWindow(string repositoryPath, string revision, Func<Window, ArgumentString, bool, Task> revert)
    {
        _module = GitModules.Open(repositoryPath);
        _revision = revision;
        _revert = revert;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormRevertCommit");
        InitialFocus.Attach(this, () => lvParentsList.IsVisible ? lvParentsList : AutoCommit);
        lvParentsList.ItemTemplate =
            new FuncDataTemplate<CherryPickParent>((parent, _) => CherryPickWindow.ParentRow(parent));
        Revert.Click += (_, _) => Run(RevertAsync);
        btnAbort.Click += (_, _) => Close(false);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the commit and its parents are shown, for tests.
    /// </summary>
    public Task RevisionLoaded => _loaded.Task;

    // Upstream's LoadRevisionInfo: the summary, and for a merge its parents with the first selected.
    private async Task LoadAsync()
    {
        try
        {
            (GitRevision? selected, bool isMerge, IReadOnlyList<GitRevision> parents) = await Task.Run(() =>
            {
                if (_module.RevParse(_revision) is not { IsZero: false } id)
                {
                    return ((GitRevision?)null, false, (IReadOnlyList<GitRevision>)[]);
                }

                bool merge = _module.IsMerge(id);
                return (_module.GetRevision(id, shortFormat: true, loadRefs: true), merge,
                    merge ? _module.GetParentRevisions(id) : []);
            });
            _selected = selected;
            _isMerge = isMerge;
            commitSummaryUserControl1.Show(selected is null ? null : CreateBranchDialog.Summarize(selected));
            ParentsLabel.IsVisible = lvParentsList.IsVisible = isMerge;
            lvParentsList.ItemsSource = parents
                .Select((parent, index) => new CherryPickParent(index + 1, parent.Subject, parent.Author ?? "",
                    parent.CommitDate.ToShortDateString()))
                .ToList();
            lvParentsList.SelectedIndex = isMerge && parents.Count > 0 ? 0 : -1;
        }
        finally
        {
            _loaded.TrySetResult();
        }
    }

    // Upstream's Revert_Click: a merge needs a parent; a message prepared before is kept above git's; the result is not
    // checked, as a failure is most likely a conflict, which the caller offers to resolve.
    private async Task RevertAsync()
    {
        int parentIndex = 0;
        if (_isMerge)
        {
            if (lvParentsList.SelectedItem is not CherryPickParent chosen)
            {
                await new MessageBoxWindow(UpstreamTranslation.ByText(CherryPickDialog.NoneParentSelected),
                    UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
                return;
            }

            parentIndex = chosen.Number;
        }

        if (_selected is null)
        {
            return;
        }

        bool autoCommit = AutoCommit.IsChecked == true;
        string prepared = await RevertCommitDialog.ReadPreparedMessageAsync(_module);
        await _revert(this, RevertCommitDialog.Arguments(_selected.ObjectId, autoCommit, parentIndex), autoCommit);
        await RevertCommitDialog.KeepPreparedMessageAsync(_module, prepared);
        Close(true);
    }

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
}
