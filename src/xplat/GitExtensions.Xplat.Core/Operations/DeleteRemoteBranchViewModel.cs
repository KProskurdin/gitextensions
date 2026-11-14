using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormDeleteRemoteBranch</c> (rules in <see cref="DeleteRemoteBranchDialog"/>): the
///  remote branches to delete, named in the box or checked in upstream's multiple branches window. Delete, enabled once
///  "Delete branch(es) from remote repository" is checked, asks first when a branch is not merged, deletes the branches
///  remote by remote through the caller, then offers the local tracking branches when asked. Closes its window with true
///  when a branch was deleted.
/// </summary>
public sealed partial class DeleteRemoteBranchViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormDeleteRemoteBranch";

    private readonly IGitModule _module;
    private readonly string? _defaultRemoteBranch;
    private readonly Func<string, IReadOnlyList<string>, Task<bool>>? _deleteOnRemote;
    private readonly Func<IReadOnlyList<string>, Task>? _deleteLocalBranches;
    private readonly Func<IReadOnlyList<string>, IReadOnlyList<string>, Task<IReadOnlyList<string>>>? _selectMultiple;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<IGitRef> _remoteRefs = [];
    private IReadOnlyList<IGitRef> _heads = [];
    private IReadOnlyCollection<string> _mergedBranches = [];

    /// <param name="module">The repository.</param>
    /// <param name="defaultRemoteBranch">The remote branch selected at first, e.g. "origin/feature".</param>
    /// <param name="deleteOnRemote">Deletes the branches (local names) on the remote, with the push scripts.</param>
    /// <param name="deleteLocalBranches">Upstream's StartDeleteBranchDialog with the local tracking branches.</param>
    /// <param name="selectMultiple">Upstream's <c>FormSelectMultipleBranches</c> over the given branches with the second
    /// ones checked; the branches checked when it closed.</param>
    public DeleteRemoteBranchViewModel(IDialogHost host, IGitModule module, string? defaultRemoteBranch,
        Func<string, IReadOnlyList<string>, Task<bool>>? deleteOnRemote,
        Func<IReadOnlyList<string>, Task>? deleteLocalBranches,
        Func<IReadOnlyList<string>, IReadOnlyList<string>, Task<IReadOnlyList<string>>>? selectMultiple)
        : base(host)
    {
        _module = module;
        _defaultRemoteBranch = defaultRemoteBranch;
        _deleteOnRemote = deleteOnRemote;
        _deleteLocalBranches = deleteLocalBranches;
        _selectMultiple = selectMultiple;
    }

    /// <summary>
    ///  The remote branches' names.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> RemoteNames { get; private set; } = [];

    /// <summary>
    ///  Upstream's <c>BranchComboBox</c> text: the branches' names with spaces between.
    /// </summary>
    [ObservableProperty]
    public partial string? BranchesText { get; set; }

    /// <summary>
    ///  Upstream's "Delete branch(es) from remote repository", which enables Delete.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool DeleteRemote { get; set; }

    [ObservableProperty]
    public partial bool DeleteLocalTrackingBranch { get; set; }

    /// <summary>
    ///  Upstream's <c>CheckDeleteTrackingAllowed</c>: a named remote branch has a local tracking branch.
    /// </summary>
    [ObservableProperty]
    public partial bool CanDeleteLocalTrackingBranch { get; private set; }

    /// <summary>
    ///  Upstream's <c>labelLocalTrackingBranches</c>: the local tracking branches that would go.
    /// </summary>
    [ObservableProperty]
    public partial string? TrackingText { get; private set; }

    /// <summary>
    ///  Completes when the branches are listed, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's constructor (the merged remote branches, read in the background) and OnRuntimeLoad, in one read: the
    ///  module's lazy values are not safe for two threads at once. The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        (_remoteRefs, _heads, _mergedBranches) = await Task.Run(() =>
            ((IReadOnlyList<IGitRef>)[.. _module.GetRefs(RefsFilter.Remotes)],
                (IReadOnlyList<IGitRef>)[.. _module.GetRefs(RefsFilter.Heads)],
                (IReadOnlyCollection<string>)[.. _module.GetMergedRemoteBranches()]));
        RemoteNames = [.. _remoteRefs.Select(gitRef => gitRef.Name)];
        BranchesText = _defaultRemoteBranch;
        CheckDeleteTrackingAllowed();
        _loaded.TrySetResult();
    });

    partial void OnBranchesTextChanged(string? value) => CheckDeleteTrackingAllowed();

    // The BranchComboBox's button: upstream's multiple branches window.
    [RelayCommand]
    private Task SelectMultipleBranchesAsync() => GuardAsync(async () =>
    {
        if (_selectMultiple is not null)
        {
            BranchesText = DeleteBranchDialog.SelectionText(await _selectMultiple(RemoteNames, await SelectedBranchesAsync()));
        }
    });

    // Upstream's Delete_Click.
    [RelayCommand(CanExecute = nameof(DeleteRemote))]
    private Task DeleteAsync() => GuardAsync(async () =>
    {
        IReadOnlyList<string> names = await SelectedBranchesAsync();
        IReadOnlyList<IGitRef> selected = [.. _remoteRefs.Where(gitRef => names.Contains(gitRef.Name))];
        if (DeleteRemoteBranchDialog.HasUnmerged(selected, _mergedBranches)
            && !await Host.ConfirmAsync(Host.Translate(DeleteRemoteBranchDialog.ConfirmDeleteUnmergedRemoteBranch),
                Host.Translate(DeleteRemoteBranchDialog.DeleteRemoteBranchesCaption), defaultIsNo: true))
        {
            return;
        }

        bool deleted = false;
        foreach ((string remote, IReadOnlyList<string> branches) in DeleteRemoteBranchDialog.ByRemote(selected))
        {
            deleted |= _deleteOnRemote is not null && await _deleteOnRemote(remote, branches);
        }

        // Upstream opens the delete branch window after each remote's push; once is enough for the same branches.
        if (deleted && DeleteLocalTrackingBranch && _deleteLocalBranches is not null)
        {
            await _deleteLocalBranches(DeleteRemoteBranchDialog.TrackingBranches(_heads, selected));
        }

        Host.Close(deleted);
    });

    // Upstream's CheckDeleteTrackingAllowed: the local tracking branches of the named remote branches, if any.
    private void CheckDeleteTrackingAllowed()
    {
        HashSet<string> names = [.. DeleteBranchDialog.Selection(BranchesText, RemoteNames).Branches];
        IReadOnlyList<string> tracking = DeleteRemoteBranchDialog.TrackingBranches(_heads,
            [.. _remoteRefs.Where(gitRef => names.Contains(gitRef.Name))]);
        if (tracking.Count == 0)
        {
            DeleteLocalTrackingBranch = false;
        }

        CanDeleteLocalTrackingBranch = tracking.Count > 0;
        TrackingText = DeleteRemoteBranchDialog.CandidatesText(tracking, Host.Translate);
    }

    private async Task<IReadOnlyList<string>> SelectedBranchesAsync()
    {
        (IReadOnlyList<string> selected, IReadOnlyList<string> unknown) =
            DeleteBranchDialog.Selection(BranchesText, RemoteNames);
        foreach (string name in unknown)
        {
            await Host.ShowMessageAsync(string.Format(Host.Translate(DeleteBranchDialog.BranchNotSelectable), name),
                Host.Translate(ManageWorktreeDialog.Error));
        }

        return selected;
    }
}
