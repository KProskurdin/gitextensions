using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Reads repository state with the shared git engine.
/// </summary>
public sealed class GitRepositoryService : IRepositoryService
{
    private readonly Func<RefSorting> _sorting;

    /// <summary>
    ///  Reads repository state; the branches come in the order <paramref name="sorting"/> asks for, read at each snapshot (by
    ///  default upstream's <c>RefsSortBy</c> and <c>RefsSortOrder</c> settings, as upstream's <c>GitModule.GetRefs</c>).
    /// </summary>
    public GitRepositoryService(Func<RefSorting>? sorting = null)
    {
        _sorting = sorting ?? (() => new RefSorting(AppSettings.RefsSortBy, AppSettings.RefsSortOrder));
    }

    public Task<RepositorySnapshot> GetSnapshotAsync(string repositoryPath)
    {
        RefSorting sorting = _sorting();
        return Task.Run(() => GetSnapshot(repositoryPath, sorting));
    }

    public Task<string> GetHeadMessageAsync(string repositoryPath) =>
        Task.Run(() => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
            .GitExecutable.GetOutput(new GitArgumentBuilder("log") { "-1", "--format=%B" }).TrimEnd());

    // Upstream's GitModule.GetWorktrees parses "worktree list --porcelain -z"; git lists the main worktree first.
    public Task<IReadOnlyList<WorktreeInfo>> GetWorktreesAsync(string repositoryPath) =>
        Task.Run(() =>
            GetWorktrees(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)));

    /// <summary>
    ///  True when HEAD contains the local branch <paramref name="branch"/>: upstream's <c>GitModule.GetMergedBranches</c>,
    ///  which its branch delete dialog uses to ask before deleting unmerged work.
    /// </summary>
    public static Task<bool> IsMergedIntoHeadAsync(string repositoryPath, string branch) =>
        Task.Run(() => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
            .GetMergedBranches().Any(merged => merged.Trim().TrimStart('*', '+').Trim() == branch));

    private static IReadOnlyList<WorktreeInfo> GetWorktrees(GitModule module)
        =>
        [
            .. module.GetWorktrees().Select((worktree, index) => new WorktreeInfo(worktree.Path,
                worktree.GetDisplayName(worktree.Path), IsMain: index == 0, worktree.IsDeleted))
        ];

    // Upstream's LocalBranchTree and RemoteBranchTree: a local branch shows its counts against its upstream, the remote
    // branch the counts reversed (the first local branch tracking it). Off when ShowAheadBehindData is off.
    private static IReadOnlyDictionary<string, string> GetAheadBehind(GitModule module)
    {
        Dictionary<string, string> texts = new(StringComparer.Ordinal);
        if (new AheadBehindDataProvider(() => module.GitExecutable).GetData() is not { } data)
        {
            return texts;
        }

        foreach (AheadBehindData item in data.Values)
        {
            texts[item.Branch] = item.ToDisplay();
            if (item.RemoteRef.StartsWith(GitRefName.RefsRemotesPrefix, StringComparison.Ordinal))
            {
                texts.TryAdd(item.RemoteRef[GitRefName.RefsRemotesPrefix.Length..], item.ToDisplay(reverse: true));
            }
        }

        return texts;
    }

    // Upstream's GitModule.GetRefs, with the sort given instead of read from upstream's settings.
    private static IReadOnlyList<IGitRef> GetRefs(GitModule module, RefsFilter filter, RefSorting sorting)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            Commands.GetRefs(filter, noLocks: true, sorting.SortBy, sorting.Order), throwOnErrorExit: false);
        return result.ExitedSuccessfully ? module.ParseRefs(result.StandardOutput) : [];
    }

    private static string? GetTrackingRemote(GitModule module, string currentBranch)
    {
        string remote = currentBranch.Length == 0
            ? ""
            : module.GetSetting(string.Format(SettingKeyString.BranchRemote, currentBranch));
        return remote.Length == 0 ? null : remote;
    }

    private static SyncStatus? GetSync(GitModule module)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            new GitArgumentBuilder("rev-list") { "--left-right", "--count", "@{u}...HEAD" }, throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            return null;
        }

        string[] counts = result.StandardOutput.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        return counts.Length == 2 && int.TryParse(counts[0], out int behind) && int.TryParse(counts[1], out int ahead)
            ? new SyncStatus(ahead, behind)
            : null;
    }

    // The list comes from .gitmodules (upstream's GetSubmodulesLocalPaths). Upstream's status parser needs git's describe
    // suffix, which "git submodule status" leaves out for a submodule that is not initialized; such a submodule has no status
    // entry and is shown as not initialized.
    private static IReadOnlyList<SubmoduleInfo> GetSubmodules(GitModule module)
    {
        IReadOnlyList<string> paths = module.GetSubmodulesLocalPaths(recursive: false);
        if (paths.Count == 0)
        {
            return [];
        }

        Dictionary<string, IGitSubmoduleInfo> status = module.GetSubmodulesInfo().OfType<IGitSubmoduleInfo>()
            .GroupBy(info => info.LocalPath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return
        [
            .. paths.Select(path => status.TryGetValue(path, out IGitSubmoduleInfo? info)
                ? new SubmoduleInfo(path, info.IsInitialized, info.IsUpToDate)
                : new SubmoduleInfo(path, IsInitialized: false, IsUpToDate: false))
        ];
    }

    public static FileChange ToFileChange(GitItemStatus status)
    {
        ChangeKind kind = status switch
        {
            { IsUnmerged: true } => ChangeKind.Conflict,
            { IsTracked: false } => ChangeKind.Untracked,
            { IsRenamed: true } => ChangeKind.Renamed,
            { IsDeleted: true } => ChangeKind.Deleted,
            { IsNew: true } => ChangeKind.Added,
            _ => ChangeKind.Modified,
        };

        return new FileChange(status.Name, kind, Staged: status.Staged == StagedStatus.Index);
    }

    private static RepositorySnapshot GetSnapshot(string path, RefSorting sorting)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), path);
        if (!module.IsValidGitWorkingDir())
        {
            throw new InvalidOperationException($"Not a git repository: {path}");
        }

        string currentBranch = module.GetSelectedBranch(emptyIfDetached: true);

        List<BranchInfo> branches =
        [
            .. GetRefs(module, RefsFilter.Heads | RefsFilter.Remotes, sorting)
                .Select(branch => new BranchInfo(branch.Name, branch.IsRemote,
                    IsCurrent: branch.IsHead && branch.Name == currentBranch)),
        ];

        List<FileChange> changes = [.. module.GetAllChangedFilesWithSubmodulesStatus().Select(ToFileChange)];

        // Upstream's InteractiveGitActionControl order: a rebase is checked before a patch, because both checks match a
        // rebase-merge folder.
        bool isMerging = module.InTheMiddleOfMerge();
        bool isRebasing = module.InTheMiddleOfRebase();
        bool isApplyingPatch = !isRebasing && module.InTheMiddleOfPatch();
        List<StashInfo> stashes = [.. module.GetStashes().Select(stash => new StashInfo(stash.Name, stash.Message))];
        List<string> tags =
        [
            .. module.GitExecutable.GetOutput(new GitArgumentBuilder("tag") { "--list" })
                .Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ];

        IReadOnlyList<string> remotes = module.GetRemoteNames();
        return new RepositorySnapshot(currentBranch.Length == 0 ? null : currentBranch, branches, changes, isMerging,
            stashes, tags, isRebasing, remotes, GetSync(module),
            GetTrackingRemote(module, currentBranch), GetSubmodules(module),
            isApplyingPatch, module.InTheMiddleOfBisect(), GetAheadBehind(module), GetWorktrees(module),
            remotes.ToDictionary(remote => remote,
                remote => module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote)),
                StringComparer.Ordinal),
            module.GetEffectiveSetting(SettingKeyString.UserEmail));
    }
}
