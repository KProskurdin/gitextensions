using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormMergeBranch</c> (rules in <see cref="MergeBranchDialog"/>): merges a branch,
///  remote branch or tag into the current branch, fast-forward or with a merge commit, optionally without committing, with
///  the advanced options; a merge that stops on conflicts offers the conflicts window, as upstream's
///  <c>MergeConflictHandler</c>. Closes its window with true after merging (or stopping on conflicts).
/// </summary>
public sealed partial class MergeBranchViewModel : DialogViewModel
{
    private readonly string _repositoryPath;
    private readonly GitModule _module;
    private readonly RepositoryOperationsViewModel _actions;
    private readonly IAppPreferences _preferences;
    private readonly IMergeSettingsStore _settings;
    private readonly string? _defaultBranch;
    private readonly Func<string, Func<Task<bool>>, Task<bool>> _withScripts;
    private readonly Action _showConflicts;
    private readonly Func<Task<bool>> _askResolveConflicts;
    private readonly TaskCompletionSource _loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <param name="repositoryPath">The repository.</param>
    /// <param name="module">The repository, for its branches, settings and merge message file.</param>
    /// <param name="actions">Runs the merge.</param>
    /// <param name="preferences">Where upstream's <c>DontCommitMerge</c> and <c>AlwaysShowAdvOpt</c> are kept.</param>
    /// <param name="settings">Where the repository's merge settings are kept.</param>
    /// <param name="defaultBranch">The branch to merge; null for the branch the current one tracks.</param>
    /// <param name="withScripts">Runs the merge between the before and after merge scripts.</param>
    /// <param name="showConflicts">Opens the conflicts window.</param>
    /// <param name="askResolveConflicts">Asks upstream's "resolve the conflicts now?" (it can be turned off).</param>
    public MergeBranchViewModel(IDialogHost host, string repositoryPath, GitModule module,
        RepositoryOperationsViewModel actions, IAppPreferences preferences, IMergeSettingsStore settings,
        string? defaultBranch, Func<string, Func<Task<bool>>, Task<bool>> withScripts, Action showConflicts,
        Func<Task<bool>> askResolveConflicts)
        : base(host)
    {
        _repositoryPath = repositoryPath;
        _module = module;
        _actions = actions;
        _preferences = preferences;
        _settings = settings;
        _defaultBranch = defaultBranch;
        _withScripts = withScripts;
        _showConflicts = showConflicts;
        _askResolveConflicts = askResolveConflicts;
        Dialog = new MergeBranchDialog(new MergeRepositorySettings(false, false, 20), noCommit: false, advanced: false);
    }

    /// <summary>
    ///  Upstream's merge strategies, in its list's order.
    /// </summary>
    public static IReadOnlyList<string> Strategies { get; } = ["resolve", "recursive", "octopus", "ours", "subtree"];

    /// <summary>
    ///  The dialog state; replaced once the repository's settings are read.
    /// </summary>
    [ObservableProperty]
    public partial MergeBranchDialog Dialog { get; private set; }

    [ObservableProperty]
    public partial string CurrentBranch { get; private set; } = "?";

    /// <summary>
    ///  The branches, remote branches and tags to merge.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Refs { get; private set; } = [];

    /// <summary>
    ///  Completes when the branches and settings are read, for tests.
    /// </summary>
    public Task Loaded => _loaded.Task;

    /// <summary>
    ///  Upstream's constructor and <c>FormMergeBranchLoad</c>: the settings, the refs to offer and the branch to propose.
    ///  The window calls it when it opens.
    /// </summary>
    public Task LoadAsync() => GuardAsync(async () =>
    {
        (string current, IReadOnlyList<string> refs, string? proposed, MergeRepositorySettings settings) =
            await Task.Run(() =>
            {
                string head = _module.GetSelectedBranch();
                IReadOnlyList<string> names =
                [
                    .. _module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes | RefsFilter.Tags)
                        .Select(gitRef => gitRef.Name)
                ];
                string? branch = _defaultBranch ?? _module.GetRemoteBranch(head);
                return (head, names, string.IsNullOrEmpty(branch) ? null : branch, _settings.Load(_module));
            });
        Dialog = new MergeBranchDialog(settings, _preferences.DontCommitMerge, _preferences.AlwaysShowAdvOpt)
        {
            Branch = proposed ?? "",
        };
        CurrentBranch = current;
        Refs = refs;
        _loaded.TrySetResult();
    });

    // Upstream's OkClick: the settings kept, the merge message written, the merge run; a merge that stops on conflicts
    // offers the conflicts window and closes too.
    [RelayCommand]
    private Task MergeAsync() => GuardAsync(async () =>
    {
        MergeBranchDialog dialog = Dialog;
        MergeRepositorySettings settings = dialog.RepositorySettings;
        await Task.Run(() => _settings.Save(_module, settings));
        _preferences.DontCommitMerge = dialog.NoCommit;
        _preferences.Save();

        string? messagePath = dialog.AddMergeMessage
            ? await MergeBranchDialog.WriteMergeMessageAsync(_module, dialog.MergeMessage)
            : null;
        bool merged = await _withScripts(_repositoryPath, () => _actions.MergeBranchAsync(_repositoryPath,
            dialog.Branch.Trim(), dialog.Arguments(messagePath, _module.GetPathForGitExecution)));
        if (merged)
        {
            Host.Close(true);
            return;
        }

        if (await Task.Run(() => _module.InTheMiddleOfConflictedMerge()))
        {
            if (await _askResolveConflicts())
            {
                _showConflicts();
            }

            Host.Close(true);
        }
    });

    [RelayCommand]
    private void Cancel() => Host.Close(false);
}
