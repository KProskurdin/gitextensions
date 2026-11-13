using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormRevertCommit</c> (rules in <see cref="RevertCommitDialog"/>): the commit to revert,
///  the parent of a merge, and whether to commit. Revert runs the caller's revert (upstream's FormProcess and conflict
///  handling) and closes the window with true.
/// </summary>
public sealed partial class RevertCommitViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormRevertCommit";

    private readonly IGitModule _module;
    private readonly string _revision;
    private readonly Func<ArgumentString, bool, Task> _revert;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private GitRevision? _selected;

    /// <param name="revision">The commit to revert.</param>
    /// <param name="revert">
    ///  Runs the revert command with its output shown and handles its conflicts as upstream's
    ///  <c>MergeConflictHandler</c> (the command, and whether a commit is offered after the conflicts).
    /// </param>
    public RevertCommitViewModel(IDialogHost host, IGitModule module, string revision, Func<ArgumentString, bool, Task> revert)
        : base(host)
    {
        _module = module;
        _revision = revision;
        _revert = revert;
        Parents = [];
    }

    /// <summary>
    ///  Upstream's <c>CommitSummaryUserControl</c> of the commit; null when git does not know it.
    /// </summary>
    [ObservableProperty]
    public partial CommitSummary? Summary { get; private set; }

    /// <summary>
    ///  The commit is a merge, so a parent is chosen.
    /// </summary>
    [ObservableProperty]
    public partial bool IsMerge { get; private set; }

    /// <summary>
    ///  A merge's parents, numbered from one.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<CherryPickParent> Parents { get; private set; }

    [ObservableProperty]
    public partial CherryPickParent? SelectedParent { get; set; }

    /// <summary>
    ///  Upstream's "Automatically create a commit".
    /// </summary>
    [ObservableProperty]
    public partial bool AutoCommit { get; set; }

    /// <summary>
    ///  Completes when the commit and its parents are shown, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's <c>LoadRevisionInfo</c>: the summary, and for a merge its parents with the first chosen. The window calls
    ///  it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        try
        {
            (GitRevision? selected, bool isMerge, IReadOnlyList<CherryPickParent> parents) =
                await Task.Run(() => CherryPickDialog.ReadCommit(_module, _revision));
            _selected = selected;
            Summary = selected is null ? null : CreateBranchDialog.Summarize(selected);
            IsMerge = isMerge;
            Parents = parents;
            SelectedParent = isMerge ? Parents.FirstOrDefault() : null;
        }
        finally
        {
            _loaded.TrySetResult();
        }
    });

    // Upstream's Revert_Click: a merge needs a parent; a message prepared before is kept above git's; the result is not
    // checked, as a failure is most likely a conflict, which the caller offers to resolve.
    [RelayCommand]
    private Task RevertAsync() => GuardAsync(async () =>
    {
        int parentIndex = 0;
        if (IsMerge)
        {
            if (SelectedParent is not { } chosen)
            {
                await Host.ShowMessageAsync(Host.Translate(CherryPickDialog.NoneParentSelected),
                    Host.Translate(ManageWorktreeDialog.Error));
                return;
            }

            parentIndex = chosen.Number;
        }

        if (_selected is null)
        {
            return;
        }

        bool autoCommit = AutoCommit;
        string prepared = await RevertCommitDialog.ReadPreparedMessageAsync(_module);
        await _revert(RevertCommitDialog.Arguments(_selected.ObjectId, autoCommit, parentIndex), autoCommit);
        await RevertCommitDialog.KeepPreparedMessageAsync(_module, prepared);
        Host.Close(true);
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);
}
