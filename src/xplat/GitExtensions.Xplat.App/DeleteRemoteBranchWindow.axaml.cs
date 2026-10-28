using Avalonia.Controls;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormDeleteRemoteBranch</c> (rules in <see cref="DeleteRemoteBranchDialog"/>): the remote branches to
///  delete, named in the box or checked in upstream's multiple branches window. Delete, enabled once "Delete branch(es)
///  from remote repository" is checked, asks first when a branch is not merged, deletes the branches remote by remote
///  through the caller, then offers the local tracking branches in the delete branch window when asked.
/// </summary>
public partial class DeleteRemoteBranchWindow : Window
{
    private readonly GitModule _module;
    private readonly string? _defaultRemoteBranch;
    private readonly Func<string, IReadOnlyList<string>, Task<bool>>? _deleteOnRemote;
    private readonly Func<Window, IReadOnlyList<string>, Task>? _deleteLocalBranches;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IReadOnlyList<IGitRef> _remoteRefs = [];
    private IReadOnlyList<IGitRef> _heads = [];
    private IReadOnlyCollection<string> _mergedBranches = [];

    public DeleteRemoteBranchWindow()
        : this("", null, null, null)
    {
    }

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="defaultRemoteBranch">The remote branch selected at first, e.g. "origin/feature".</param>
    /// <param name="deleteOnRemote">Deletes the branches (local names) on the remote, with the push scripts.</param>
    /// <param name="deleteLocalBranches">Upstream's StartDeleteBranchDialog with the local tracking branches.</param>
    public DeleteRemoteBranchWindow(string repositoryPath, string? defaultRemoteBranch,
        Func<string, IReadOnlyList<string>, Task<bool>>? deleteOnRemote,
        Func<Window, IReadOnlyList<string>, Task>? deleteLocalBranches)
    {
        _module = GitModules.Open(repositoryPath);
        _defaultRemoteBranch = defaultRemoteBranch;
        _deleteOnRemote = deleteOnRemote;
        _deleteLocalBranches = deleteLocalBranches;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormDeleteRemoteBranch");
        InitialFocus.Attach(this, () => Branches);
        DeleteRemote.IsCheckedChanged += (_, _) => Delete.IsEnabled = DeleteRemote.IsChecked == true;
        Branches.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty)
            {
                CheckDeleteTrackingAllowed();
            }
        };
        selectMultipleBranchesButton.Click += (_, _) => Run(SelectMultipleBranchesAsync);
        Delete.Click += (_, _) => Run(DeleteAsync);
        Opened += (_, _) => Run(LoadAsync);
    }

    /// <summary>
    ///  Completes when the branches are listed, for tests.
    /// </summary>
    public Task BranchesLoaded => _loaded.Task;

    private IReadOnlyList<string> RemoteNames => [.. _remoteRefs.Select(gitRef => gitRef.Name)];

    private void Run(Func<Task> action)
        => UiActions.Run(action, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));

    // Upstream's constructor (the merged remote branches, read in the background) and OnRuntimeLoad, in one read: the
    // module's lazy values are not safe for two threads at once.
    private async Task LoadAsync()
    {
        (_remoteRefs, _heads, _mergedBranches) = await Task.Run(() =>
            ((IReadOnlyList<IGitRef>)[.. _module.GetRefs(RefsFilter.Remotes)],
                (IReadOnlyList<IGitRef>)[.. _module.GetRefs(RefsFilter.Heads)],
                (IReadOnlyCollection<string>)[.. _module.GetMergedRemoteBranches()]));
        Branches.ItemsSource = RemoteNames;
        Branches.Text = _defaultRemoteBranch;
        CheckDeleteTrackingAllowed();
        _loaded.TrySetResult();
    }

    // Upstream's CheckDeleteTrackingAllowed: the local tracking branches of the named remote branches, if any.
    private void CheckDeleteTrackingAllowed()
    {
        IReadOnlyList<string> tracking = DeleteRemoteBranchDialog.TrackingBranches(_heads, SelectedRefs(Branches.Text));
        if (tracking.Count == 0)
        {
            DeleteLocalTrackingBranch.IsChecked = false;
        }

        DeleteLocalTrackingBranch.IsEnabled = tracking.Count > 0;
        _NO_TRANSLATE_labelLocalTrackingBranches.Text =
            DeleteRemoteBranchDialog.CandidatesText(tracking, UpstreamTranslation.ByText);
    }

    private IReadOnlyList<IGitRef> SelectedRefs(string? text)
    {
        HashSet<string> names = [.. DeleteBranchDialog.Selection(text, RemoteNames).Branches];
        return [.. _remoteRefs.Where(gitRef => names.Contains(gitRef.Name))];
    }

    // The BranchComboBox's button: upstream's multiple branches window.
    private async Task SelectMultipleBranchesAsync()
    {
        SelectMultipleBranchesWindow window = new(RemoteNames, await SelectedBranchesAsync());
        await window.ShowDialog(this);
        Branches.Text = DeleteBranchDialog.SelectionText(window.SelectedBranches);
    }

    // Upstream's Delete_Click.
    private async Task DeleteAsync()
    {
        if (DeleteRemote.IsChecked != true)
        {
            return;
        }

        IReadOnlyList<string> names = await SelectedBranchesAsync();
        IReadOnlyList<IGitRef> selected = [.. _remoteRefs.Where(gitRef => names.Contains(gitRef.Name))];
        if (DeleteRemoteBranchDialog.HasUnmerged(selected, _mergedBranches))
        {
            MessageBoxWindow question = new(UpstreamTranslation.ByText(DeleteRemoteBranchDialog.ConfirmDeleteUnmergedRemoteBranch),
                UpstreamTranslation.ByText(DeleteRemoteBranchDialog.DeleteRemoteBranchesCaption), MessageBoxButtons.YesNo,
                MessageBoxDefaultButton.Button2);
            await question.ShowDialog(this);
            if (question.Result != DialogResult.Yes)
            {
                return;
            }
        }

        bool deleted = false;
        foreach ((string remote, IReadOnlyList<string> branches) in DeleteRemoteBranchDialog.ByRemote(selected))
        {
            deleted |= _deleteOnRemote is not null && await _deleteOnRemote(remote, branches);
        }

        // Upstream opens the delete branch window after each remote's push; once is enough for the same branches.
        if (deleted && DeleteLocalTrackingBranch.IsChecked == true && _deleteLocalBranches is not null)
        {
            await _deleteLocalBranches(this, DeleteRemoteBranchDialog.TrackingBranches(_heads, selected));
        }

        Close(deleted);
    }

    private async Task<IReadOnlyList<string>> SelectedBranchesAsync()
    {
        (IReadOnlyList<string> selected, IReadOnlyList<string> unknown) =
            DeleteBranchDialog.Selection(Branches.Text, RemoteNames);
        foreach (string name in unknown)
        {
            await new MessageBoxWindow(
                string.Format(UpstreamTranslation.ByText(DeleteBranchDialog.BranchNotSelectable), name),
                UpstreamTranslation.ByText(ManageWorktreeDialog.Error), MessageBoxButtons.OK).ShowDialog(this);
        }

        return selected;
    }
}
