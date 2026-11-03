using System.Text;
using GitCommands;
using GitCommands.Git;
using GitCommands.Submodules;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using ResourceManager;
using UpstreamSubmoduleInfo = GitCommands.Submodules.SubmoduleInfo;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A project of upstream's submodule tree (<c>SubmoduleNode</c>): the top project or one of the submodules below it, with
///  the status upstream's <c>SubmoduleStatusProvider</c> read (<c>DetailedSubmoduleInfo</c>).
/// </summary>
/// <param name="Text">Upstream's <c>SubmoduleInfo.Text</c>: the path below the top project (the top project's folder name),
///  then the checked out branch in parentheses when upstream's <c>ShowRepoCurrentBranch</c> is on.</param>
/// <param name="Path">The project's full path, with upstream's trailing separator.</param>
/// <param name="IsCurrent">The project is the repository the browse window shows (upstream's <c>SubmoduleInfo.Bold</c>).</param>
/// <param name="LocalPath">The path below <paramref name="SuperPath"/>, posix style; empty for the top project.</param>
/// <param name="SuperPath">The full path of the project that holds this one; the top project's own path for itself.</param>
public sealed record SubmoduleProject(string Text, string Path, bool IsCurrent, string LocalPath, string SuperPath)
{
    /// <summary>
    ///  Upstream's <c>DetailedSubmoduleInfo.Status</c>; null when the project has no details (no change was read).
    /// </summary>
    public SubmoduleStatus? Status { get; init; }

    /// <summary>
    ///  Upstream's <c>DetailedSubmoduleInfo.IsDirty</c>: the project has changes that are not committed.
    /// </summary>
    public bool IsDirty { get; init; }

    /// <summary>
    ///  Upstream's <c>DetailedSubmoduleInfo.AddedAndRemovedText</c>, such as " (+2 -1)", after the name.
    /// </summary>
    public string AddedAndRemoved { get; init; } = "";

    /// <summary>
    ///  Upstream's tooltip (<c>SubmoduleNode.SetStatusToolTipAsync</c>), read with the structure.
    /// </summary>
    public string ToolTip { get; init; } = "";

    /// <summary>
    ///  Upstream's <c>SubmoduleNode.SubmoduleName</c>: the last folder of the path.
    /// </summary>
    public string Name => PathAndBranch[0].SubstringAfterLast('/');

    /// <summary>
    ///  Upstream's <c>SubmoduleNode.DisplayText</c>: the name, the branch and the added and removed commits.
    /// </summary>
    public string DisplayText => Name + (PathAndBranch.Length == 2 ? $" {PathAndBranch[1]}" : "") + AddedAndRemoved;

    /// <summary>
    ///  Upstream's <c>GetSubmoduleItemImage</c>: by the status and whether the project is dirty.
    /// </summary>
    public string Image => (Status, IsDirty) switch
    {
        (null, _) => "FolderSubmodule",
        (SubmoduleStatus.FastForward, true) => "SubmoduleRevisionUpDirty",
        (SubmoduleStatus.FastForward, false) => "SubmoduleRevisionUp",
        (SubmoduleStatus.Rewind, true) => "SubmoduleRevisionDownDirty",
        (SubmoduleStatus.Rewind, false) => "SubmoduleRevisionDown",
        (SubmoduleStatus.NewerTime, true) => "SubmoduleRevisionSemiUpDirty",
        (SubmoduleStatus.NewerTime, false) => "SubmoduleRevisionSemiUp",
        (SubmoduleStatus.OlderTime, true) => "SubmoduleRevisionSemiDownDirty",
        (SubmoduleStatus.OlderTime, false) => "SubmoduleRevisionSemiDown",
        (_, true) => "SubmoduleDirty",
        (_, false) => "FileStatusModified",
    };

    // Upstream splits "Externals/conemu-inside (no branch)" at its first space.
    private string[] PathAndBranch => Text.Split(' ', 2);
}

/// <summary>
///  What upstream's submodule tree shows: the top project of the repository and every submodule below it, recursively, as
///  upstream's <c>SubmoduleStatusProvider</c> reads them (also when the repository is itself a submodule).
/// </summary>
/// <param name="Top">The top-most project.</param>
/// <param name="Submodules">The submodules below <paramref name="Top"/>, recursively, in upstream's order.</param>
/// <param name="IsBareRepository">The repository the browse window shows is bare: upstream then offers no reset, stash,
///  commit, manage or synchronize.</param>
public sealed record SubmoduleStructure(SubmoduleProject Top, IReadOnlyList<SubmoduleProject> Submodules,
    bool IsBareRepository)
{
    // Upstream's TranslatedStrings.NoBranch.
    public const string NoBranchText = "no branch";

    // Upstream's FormBrowse texts of its submodule drop-down.
    public const string NoSubmodulesText = "No submodules";
    public const string TopProjectFormat = "Top project: {0}";
    public const string SuperprojectFormat = "Superproject: {0}";
    public const string GoToSuperprojectText = "Go to superproject";
    public const string LoadingText = "Loading...";
    public const string UpdateAllSubmodulesText = "&Update all submodules";
    public const string UpdateCurrentSubmoduleText = "Update current submodule";

    /// <summary>
    ///  Upstream's <c>SubmoduleInfoResult.OurSubmodules</c>: the submodules below the shown repository, recursively.
    /// </summary>
    public IReadOnlyList<SubmoduleProject> OurSubmodules { get; init; } = [];

    /// <summary>
    ///  Upstream's <c>SubmoduleInfoResult.AllSubmodules</c>: every submodule below <see cref="Top"/>, also those
    ///  <see cref="Submodules"/> leaves out because the project holding them is missing.
    /// </summary>
    public IReadOnlyList<SubmoduleProject> AllSubmodules { get; init; } = [];

    /// <summary>
    ///  Upstream's <c>SubmoduleInfoResult.SuperProject</c>: the project holding the shown repository; null when the shown
    ///  repository is the top project.
    /// </summary>
    public SubmoduleProject? SuperProject { get; init; }

    /// <summary>
    ///  Upstream's <c>SubmoduleInfoResult.CurrentSubmoduleName</c>: the shown repository's path in its superproject, when
    ///  upstream found it among the top project's submodules.
    /// </summary>
    public string? CurrentSubmoduleName { get; init; }

    /// <summary>
    ///  Upstream's <c>FormBrowse.PopulateToolbar</c>: the submodules below the shown repository (or "No submodules"); in a
    ///  submodule then the top project (unless it is the superproject), the superproject and all the submodules; then
    ///  "Update all submodules" and, in a submodule, "Update current submodule".
    /// </summary>
    public IReadOnlyList<SubmoduleMenuItem> MenuItems(Func<string, string> translate)
    {
        List<SubmoduleMenuItem> items = [.. OurSubmodules.Select(PlainItem)];
        if (OurSubmodules.Count == 0)
        {
            items.Add(new SubmoduleMenuItem(SubmoduleMenuItemKind.NoSubmodules, translate(NoSubmodulesText)));
        }

        if (SuperProject is not null)
        {
            items.Add(new SubmoduleMenuItem(SubmoduleMenuItemKind.Separator));
            if (Top != SuperProject)
            {
                items.Add(Item(Top, translate(TopProjectFormat)));
            }

            items.Add(Item(SuperProject, translate(SuperprojectFormat)));
            items.AddRange(AllSubmodules.Select(PlainItem));
        }

        items.Add(new SubmoduleMenuItem(SubmoduleMenuItemKind.Separator));
        items.Add(new SubmoduleMenuItem(SubmoduleMenuItemKind.UpdateAll, translate(UpdateAllSubmodulesText)));
        if (CurrentSubmoduleName is not null)
        {
            items.Add(new SubmoduleMenuItem(SubmoduleMenuItemKind.UpdateCurrent, translate(UpdateCurrentSubmoduleText)));
        }

        return items;

        // Upstream's CreateSubmoduleMenuItem and UpdateSubmoduleMenuItemStatus: the text and the added and removed commits.
        static SubmoduleMenuItem Item(SubmoduleProject project, string format)
            => new(SubmoduleMenuItemKind.Project, string.Format(format, project.Text + project.AddedAndRemoved), project);

        static SubmoduleMenuItem PlainItem(SubmoduleProject project) => Item(project, "{0}");
    }

    /// <summary>
    ///  The same projects with the same status, so that nothing shown from <paramref name="other"/> would change.
    /// </summary>
    public bool SameAs(SubmoduleStructure other)
        => Top == other.Top && IsBareRepository == other.IsBareRepository && SuperProject == other.SuperProject
           && CurrentSubmoduleName == other.CurrentSubmoduleName && Submodules.SequenceEqual(other.Submodules)
           && AllSubmodules.SequenceEqual(other.AllSubmodules) && OurSubmodules.SequenceEqual(other.OurSubmodules);

    /// <summary>
    ///  Reads the structure of <paramref name="repositoryPath"/> with upstream's <c>SubmoduleStatusProvider</c>: its
    ///  <c>UpdateSubmodulesStructureAsync</c>, then, when <paramref name="showStatus"/> (upstream's
    ///  <c>ShowSubmoduleStatus</c>), its <c>UpdateSubmodulesStatusAsync</c> with the repository's git status, as upstream's
    ///  browse window calls them; then each project's tooltip.
    /// </summary>
    public static Task<SubmoduleStructure> LoadAsync(string repositoryPath, string noBranchText, bool showStatus,
        CancellationToken cancellationToken)
        => Task.Run(async () =>
        {
            GitExecutorProvider executors = new(new GitDirectoryResolver());
            GitModule module = new(executors, repositoryPath);
            using SubmoduleStatusProvider provider = new(executors);
            SubmoduleInfoResult? result = null;
            provider.StatusUpdated += (_, e) => result = e.Info;
            await provider.UpdateSubmodulesStructureAsync(module.WorkingDir, noBranchText, showStatus);
            if (showStatus)
            {
                await provider.UpdateSubmodulesStatusAsync(module.WorkingDir, GitRepositoryService.ChangedFiles(module),
                    forceUpdate: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return result?.TopProject is { } top
                ? From(result, top, module, executors, cancellationToken)
                : throw new InvalidOperationException($"No submodule structure read for {repositoryPath}");
        }, cancellationToken);

    /// <summary>
    ///  Upstream's <c>ArtificialCommitChangeCount.GetSummary</c>, the current project's tooltip: the changed, deleted and
    ///  new files and the changed and dirty submodules, at most five of each.
    /// </summary>
    public static string ChangeSummary(IReadOnlyList<GitItemStatus> items)
    {
        const int maxItems = 5;
        StringBuilder builder = new();
        Append([.. items.Where(item => !item.IsNew && !item.IsDeleted && !item.IsSubmodule)], "changed file");
        Append([.. items.Where(item => item.IsDeleted && !item.IsSubmodule)], "deleted file");
        Append([.. items.Where(item => item.IsNew && !item.IsSubmodule)], "new file");
        Append([.. items.Where(item => item.IsSubmodule && item.IsChanged)], "changed submodule");
        Append([.. items.Where(item => item.IsSubmodule && item.IsDirty)], "dirty submodule");
        return builder.ToString();

        void Append(IReadOnlyList<GitItemStatus> group, string singular)
        {
            if (group.Count == 0)
            {
                return;
            }

            if (builder.Length != 0)
            {
                builder.AppendLine();
            }

            builder.Append($"{group.Count} {singular}{(group.Count == 1 ? "" : "s")}").AppendLine();
            foreach (GitItemStatus item in group.Take(maxItems))
            {
                builder.Append("- ").AppendLine(item.Name);
            }

            if (group.Count > maxItems)
            {
                int unlisted = group.Count - maxItems;
                builder.Append("- (").Append(unlisted).Append(" more file").Append(unlisted == 1 ? "" : "s").Append(')')
                    .AppendLine();
            }
        }
    }

    // Upstream's SubmoduleTree.CreateSubmoduleNodes: the project holding a submodule is the nearest one whose path its path
    // contains, among the submodules, the current module and its superprojects. A submodule whose holder does not exist is
    // left out; upstream also shows a message box for it.
    private static SubmoduleStructure From(SubmoduleInfoResult result, UpstreamSubmoduleInfo top, GitModule module,
        GitExecutorProvider executors, CancellationToken cancellationToken)
    {
        List<string> modulePaths = [.. result.AllSubmodules.Select(info => info.Path)];
        for (IGitModule? parent = module; parent is not null; parent = parent.SuperprojectModule)
        {
            modulePaths.Add(parent.WorkingDir);
        }

        modulePaths = [.. modulePaths.OrderByDescending(path => path)];
        SubmoduleProject topProject = Project(top, "", top.Path, result, executors);
        Dictionary<UpstreamSubmoduleInfo, SubmoduleProject> projects = new(ReferenceEqualityComparer.Instance) { [top] = topProject };
        List<SubmoduleProject> submodules = [];
        foreach (UpstreamSubmoduleInfo info in result.AllSubmodules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? superPath = modulePaths.Find(path => info.Path != path && info.Path.Contains(path));
            string? holder = superPath is not null && Directory.Exists(superPath) ? superPath : null;
            string localPath = holder is null ? "" : Path.GetDirectoryName(info.Path[holder.Length..]).ToPosixPath() ?? "";
            SubmoduleProject project = Project(info, localPath, holder ?? "", result, executors);
            projects[info] = project;
            if (holder is not null)
            {
                submodules.Add(project);
            }
        }

        return new SubmoduleStructure(topProject, submodules, module.IsBareRepository())
        {
            AllSubmodules = [.. result.AllSubmodules.Select(info => projects[info])],
            OurSubmodules = [.. result.OurSubmodules.Select(info => projects[info])],
            SuperProject = result.SuperProject is { } super ? projects[super] : null,
            CurrentSubmoduleName = result.CurrentSubmoduleName,
        };
    }

    private static SubmoduleProject Project(UpstreamSubmoduleInfo info, string localPath, string superPath,
        SubmoduleInfoResult result, GitExecutorProvider executors)
        => new(info.Text, info.Path, info.Bold, localPath, superPath)
        {
            Status = info.Detailed?.Status,
            IsDirty = info.Detailed?.IsDirty ?? false,
            AddedAndRemoved = info.Detailed?.AddedAndRemovedText ?? "",
            ToolTip = ToolTipOf(info, info.Bold ? result.CurrentSubmoduleStatus : null, executors),
        };

    // Upstream's SubmoduleNode.SetStatusToolTipAsync: the submodule status (with the commits ahead and behind), else for the
    // current project its changes, else the project's last commit.
    private static string ToolTipOf(UpstreamSubmoduleInfo info, IReadOnlyList<GitItemStatus>? gitStatus,
        GitExecutorProvider executors)
    {
        if (info.Detailed?.RawStatus is { } status)
        {
            return SubmoduleResources.GetSubmoduleStatusText(new GitModule(executors, info.Path), status,
                moduleIsParent: false, limitOutput: true);
        }

        return gitStatus is not null
            ? ChangeSummary(gitStatus)
            : SubmoduleResources.GetSubmoduleText(new GitModule(executors, "."), info.Path, hash: "");
    }
}

/// <summary>
///  The kinds of item in upstream's submodule drop-down (<c>FormBrowse.PopulateToolbar</c>).
/// </summary>
public enum SubmoduleMenuItemKind
{
    /// <summary>
    ///  A project, opened in the browse window.
    /// </summary>
    Project,

    /// <summary>
    ///  Upstream's "No submodules", which does nothing.
    /// </summary>
    NoSubmodules,

    Separator,

    /// <summary>
    ///  Upstream's <c>StartUpdateSubmodulesDialog</c> on the shown repository.
    /// </summary>
    UpdateAll,

    /// <summary>
    ///  Upstream's <c>UpdateSubmoduleToolStripMenuItemClick</c>: the shown repository updated from its superproject.
    /// </summary>
    UpdateCurrent,
}

/// <summary>
///  An item of upstream's submodule drop-down; <paramref name="Project"/> for a project item, shown in bold when it is the
///  shown repository and with its status image.
/// </summary>
public sealed record SubmoduleMenuItem(SubmoduleMenuItemKind Kind, string Text = "", SubmoduleProject? Project = null);
