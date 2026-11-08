using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitCommands.Remotes;
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
    ///  HEAD's commit; the zero id before the first commit.
    /// </summary>
    public static ObjectId HeadId(string repositoryPath)
        => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath).RevParse("HEAD");

    /// <summary>
    ///  True when HEAD contains the local branch <paramref name="branch"/>: upstream's <c>GitModule.GetMergedBranches</c>,
    ///  which its branch delete dialog uses to ask before deleting unmerged work.
    /// </summary>
    public static Task<bool> IsMergedIntoHeadAsync(string repositoryPath, string branch) =>
        Task.Run(() => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
            .GetMergedBranches().Any(merged => merged.Trim().TrimStart('*', '+').Trim() == branch));

    /// <summary>
    ///  Upstream FormCommit's <c>ComputeUnstagedFiles</c> with its list's settings: the working directory's and the
    ///  index's changes, with the ignored, assumed-unchanged and skip-worktree files when asked for.
    /// </summary>
    public static Task<IReadOnlyList<FileChange>> GetWorkTreeChangesAsync(string repositoryPath, bool showIgnored,
        bool showAssumeUnchanged, bool showSkipWorktree) =>
        Task.Run<IReadOnlyList<FileChange>>(() =>
        [
            .. new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
                .GetAllChangedFilesWithSubmodulesStatus(excludeIgnoredFiles: !showIgnored,
                    excludeAssumeUnchangedFiles: !showAssumeUnchanged, excludeSkipWorktreeFiles: !showSkipWorktree,
                    UntrackedFilesMode.Default, CancellationToken.None)
                .Where(status => !status.IsStatusOnly)
                .Select(ToFileChange)
        ]);

    /// <summary>
    ///  The full names of the local and remote branches that <paramref name="commit"/> contains: upstream's
    ///  <c>GitModule.GetMergedBranchesAsync(includeRemote: true, fullRefname: true, commit)</c>, which its left panel reads
    ///  for the grid's selected commit.
    /// </summary>
    public static Task<string[]> GetMergedBranchesAsync(string repositoryPath, string commit,
        CancellationToken cancellationToken) =>
        Task.Run(() => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
            .GetMergedBranchesAsync(includeRemote: true, fullRefname: true, commit, cancellationToken), cancellationToken);

    private static IReadOnlyList<WorktreeInfo> GetWorktrees(GitModule module)
        =>
        [
            .. module.GetWorktrees().Select((worktree, index) => new WorktreeInfo(worktree.Path,
                worktree.GetDisplayName(worktree.Path), IsMain: index == 0, worktree.IsDeleted, worktree.HeadType,
                worktree.Branch, worktree.Sha1))
        ];

    // Upstream's LocalBranchTree and RemoteBranchTree: a local branch shows its counts against its upstream, the remote
    // branch the counts reversed (the first local branch tracking it). Off when ShowAheadBehindData is off. With them
    // upstream's RelatedBranch (UpdateAheadBehind): the local branch's upstream, the remote branch's local branch.
    private static (IReadOnlyDictionary<string, string> Texts, IReadOnlyDictionary<string, string> Related) GetAheadBehind(
        GitModule module)
    {
        Dictionary<string, string> texts = new(StringComparer.Ordinal);
        Dictionary<string, string> related = new(StringComparer.Ordinal);
        if (new AheadBehindDataProvider(() => module.GitExecutable).GetData() is not { } data)
        {
            return (texts, related);
        }

        foreach (AheadBehindData item in data.Values)
        {
            texts[item.Branch] = item.ToDisplay();
            related[item.Branch] = item.RemoteRef;
            if (item.RemoteRef.StartsWith(GitRefName.RefsRemotesPrefix, StringComparison.Ordinal))
            {
                string remoteBranch = item.RemoteRef[GitRefName.RefsRemotesPrefix.Length..];
                if (texts.TryAdd(remoteBranch, item.ToDisplay(reverse: true)))
                {
                    related[remoteBranch] = GitRefName.RefsHeadsPrefix + item.Branch;
                }
            }
        }

        return (texts, related);
    }

    /// <summary>
    ///  Upstream FormBrowse's branch drop-down (<c>CurrentBranchDropDownOpening</c>): the local branches as
    ///  <c>GitModule.GetRefs(RefsFilter.Heads)</c> lists them, in <paramref name="sorting"/>, at most upstream's 100.
    /// </summary>
    public static Task<IReadOnlyList<(string Name, string Hash)>> GetBranchMenuBranchesAsync(string repositoryPath,
        RefSorting sorting) =>
        Task.Run<IReadOnlyList<(string Name, string Hash)>>(() =>
        [
            .. GetRefs(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath), RefsFilter.Heads,
                    sorting)
                .Take(MaxBranchMenuItems)
                .Select(branch => (branch.Name, branch.ObjectId.ToString())),
        ]);

    // Upstream's limit: the drop-down hangs when it is too large.
    private const int MaxBranchMenuItems = 100;

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

        return new FileChange(status.Name, kind, Staged: status.Staged == StagedStatus.Index)
        {
            OldPath = status.IsRenamed ? status.OldName : null,
            IsSkipWorktree = status.IsSkipWorktree,
            IsAssumeUnchanged = status.IsAssumeUnchanged,
            IsSubmodule = status.IsSubmodule,
            IsSubmoduleChanged = status.IsSubmodule && status.IsChanged,
            IsSubmoduleDirty = status.IsSubmodule && status.IsDirty,
        };
    }

    /// <summary>
    ///  Upstream's <c>SkipWorktree_Click</c> and <c>AssumeUnchanged_Click</c>: git's skip-worktree or assume-unchanged flag
    ///  set or cleared on the files that do not have it as asked (upstream's <c>SkipWorktreeFiles</c> and
    ///  <c>AssumeUnchangedFiles</c>); false with git's output when git failed.
    /// </summary>
    public static Task<(bool Done, string Output)> SetIndexFlagAsync(string repositoryPath, IReadOnlyList<FileChange> files,
        IndexFlag flag, bool value) =>
        Task.Run(() =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            List<GitItemStatus> statuses =
            [
                .. files.Select(file => new GitItemStatus(file.Path)
                {
                    IsTracked = true,
                    IsSkipWorktree = file.IsSkipWorktree,
                    IsAssumeUnchanged = file.IsAssumeUnchanged
                })
            ];
            string output;
            bool done = flag == IndexFlag.SkipWorktree
                ? module.SkipWorktreeFiles(statuses, value, out output)
                : module.AssumeUnchangedFiles(statuses, value, out output);
            return (done, output);
        });

    // Upstream's GitStatusMonitor reads the status with --no-optional-locks: a refresh, such as when the window is activated
    // again, then never holds the index lock while a command the user started needs it ("index.lock: File exists"). A
    // failed read is read again as GitModule reads it, which lists the error as a file.
    internal static IReadOnlyList<GitItemStatus> ChangedFiles(GitModule module)
    {
        ExecutionResult result = module.GitExecutable.Execute(
            Commands.GetAllChangedFiles(excludeIgnoredFiles: true, UntrackedFilesMode.Default, noLocks: true),
            throwOnErrorExit: false);
        if (!result.ExitedSuccessfully)
        {
            return module.GetAllChangedFilesWithSubmodulesStatus();
        }

        IReadOnlyList<GitItemStatus> status = new GetAllChangedFilesOutputParser(() => module).Parse(result.StandardOutput);
        module.GetSubmoduleCurrentStatus(status);
        return status;
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

        List<FileChange> changes = [.. ChangedFiles(module).Select(ToFileChange)];

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
        IReadOnlyList<Remote> inactiveRemotes = new ConfigFileRemoteSettingsManager(() => module).GetDisabledRemotes();
        Dictionary<string, string> remoteUrls = remotes.ToDictionary(remote => remote,
            remote => module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote)), StringComparer.Ordinal);
        foreach (Remote inactive in inactiveRemotes)
        {
            remoteUrls.TryAdd(inactive.Name, inactive.FetchUrl);
        }

        (IReadOnlyDictionary<string, string> aheadBehind, IReadOnlyDictionary<string, string> related) =
            GetAheadBehind(module);
        return new RepositorySnapshot(currentBranch.Length == 0 ? null : currentBranch, branches, changes, isMerging,
            stashes, tags, isRebasing, remotes, GetSync(module),
            GetTrackingRemote(module, currentBranch), GetSubmodules(module),
            isApplyingPatch, module.InTheMiddleOfBisect(), aheadBehind, GetWorktrees(module),
            remoteUrls, module.GetEffectiveSetting(SettingKeyString.UserEmail),
            [.. inactiveRemotes.Select(inactive => inactive.Name)], module.GetRemoteColors(), related);
    }
}
