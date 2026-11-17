using CommunityToolkit.Mvvm.ComponentModel;
using GitExtensions.Xplat.Core.Presentation;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  What the left panel's context menu opens on, as the window reads it.
/// </summary>
/// <param name="Roots">The tree's root nodes, which hold the marked nodes and the node's root.</param>
/// <param name="Node">The selected node.</param>
/// <param name="IsOpen">A repository is open and nothing is in progress.</param>
/// <param name="SelectedRevisionCount">The commits selected in the grid, which Copy takes.</param>
/// <param name="RemoteCount">The repository's remotes.</param>
/// <param name="HasRemoteWebUrl">The selected remote's address is a web address.</param>
/// <param name="IsBareRepository">The repository shown is bare.</param>
/// <param name="RefsSortBy">The order of the refs (upstream's <c>RefsSortBy</c>).</param>
/// <param name="RepositoryPath">The open repository, which the commands act in.</param>
/// <param name="RemoteWebUrl">The selected remote's web address, which "Open remote URL" opens.</param>
public sealed record RepoObjectMenuInput(
    IReadOnlyList<RepoObjectNode> Roots,
    RepoObjectNode? Node,
    bool IsOpen,
    int SelectedRevisionCount = 0,
    int RemoteCount = 0,
    bool HasRemoteWebUrl = false,
    bool IsBareRepository = false,
    GitRefsSortBy RefsSortBy = GitRefsSortBy.Default,
    string? RepositoryPath = null,
    Uri? RemoteWebUrl = null);

/// <summary>
///  The left panel's context menu state (upstream's <c>RepoObjectsTree.contextMenu_Opening</c>, its
///  <c>MenuItemsGenerator</c> and the <c>Enable...ContextMenu</c> methods, PLAN.md principle 6): which items apply to the
///  node it opens on, which of them can run, and the ref items' texts by the kind of ref. The view fills the copy and script
///  submenus and hides the separators left alone.
/// </summary>
public sealed partial class RepoObjectMenuViewModel : ObservableObject
{
    // Upstream's texts that depend on the kind of ref (BranchMenuItemsStrings, RemoteBranchMenuItemsStrings and
    // TagMenuItemsStrings).
    public const string CheckoutBranchText = "Chec_kout branch...";
    public const string CheckoutRemoteBranchText = "Chec_kout remote branch...";
    public const string CheckoutTagText = "Chec_kout tag revision...";
    public const string RebaseOnBranchText = "_Rebase current branch on this branch...";
    public const string RebaseOnRemoteBranchText = "_Rebase current branch on this remote branch...";
    public const string RebaseOnTagText = "_Rebase current branch on this tag revision...";
    public const string DeleteBranchText = "_Delete branch...";
    public const string DeleteRemoteBranchText = "_Delete remote branch...";
    public const string DeleteTagText = "_Delete tag...";
    public const string CheckoutBranchTooltip = "Checkout this branch";
    public const string CheckoutTagTooltip = "Checkout this tag revision";
    public const string MergeBranchTooltip = "Merge this branch into current branch";
    public const string MergeTagTooltip = "Merge this tag into current branch";
    public const string CreateFromBranchTooltip = "Create a local branch from this branch";
    public const string CreateFromTagTooltip = "Create a local branch from this tag";
    public const string RebaseOnBranchTooltip = "Rebase current branch on this branch";
    public const string RebaseOnTagTooltip = "Rebase current branch on this tag";
    public const string DeleteBranchTooltip =
        "Delete the branch, which must be fully merged in its upstream branch or in HEAD";

    public const string DeleteRemoteBranchTooltip = "Delete the branch from the remote";
    public const string DeleteTagTooltip = "Delete this tag";

    private readonly Func<string, string> _translate;
    private readonly Func<string, bool> _directoryExists;

    /// <param name="translate">Upstream's translation of an English text.</param>
    /// <param name="directoryExists">Whether a folder exists; the default looks on disk.</param>
    public RepoObjectMenuViewModel(Func<string, string>? translate = null, Func<string, bool>? directoryExists = null)
    {
        _translate = translate ?? (text => text);
        _directoryExists = directoryExists ?? Directory.Exists;
        Checkout = new MenuItemState(_translate(CheckoutBranchText), _translate(CheckoutBranchTooltip));
        Merge = new MenuItemState(tip: _translate(MergeBranchTooltip));
        Rebase = new MenuItemState(_translate(RebaseOnBranchText), _translate(RebaseOnBranchTooltip));
        CreateBranch = new MenuItemState(tip: _translate(CreateFromBranchTooltip));
        Delete = new MenuItemState(_translate(DeleteBranchText), _translate(DeleteBranchTooltip));
    }

    public MenuItemState Copy { get; } = new();

    public MenuItemState Filter { get; } = new();

    public MenuItemState Checkout { get; }

    public MenuItemState Merge { get; }

    public MenuItemState Rebase { get; }

    public MenuItemState CreateBranch { get; }

    public MenuItemState Reset { get; } = new();

    public MenuItemState RenameBranch { get; } = new();

    public MenuItemState Delete { get; }

    public MenuItemState DeleteRemoteTag { get; } = new();

    public MenuItemState ManageRemotes { get; } = new();

    public MenuItemState FetchAllRemotes { get; } = new();

    public MenuItemState PruneAllRemotes { get; } = new();

    public MenuItemState ManageRemote { get; } = new();

    public MenuItemState ActivateRemote { get; } = new();

    public MenuItemState ActivateAndFetchRemote { get; } = new();

    public MenuItemState DeactivateRemote { get; } = new();

    public MenuItemState FetchRemote { get; } = new();

    public MenuItemState PruneRemote { get; } = new();

    public MenuItemState OpenRemoteUrl { get; } = new();

    public MenuItemState OpenSubmodule { get; } = new();

    public MenuItemState OpenSubmoduleNewInstance { get; } = new();

    public MenuItemState ManageSubmodules { get; } = new();

    public MenuItemState UpdateSubmodule { get; } = new();

    public MenuItemState SynchronizeSubmodule { get; } = new();

    public MenuItemState ResetSubmodule { get; } = new();

    public MenuItemState StashSubmodule { get; } = new();

    public MenuItemState CommitSubmodule { get; } = new();

    public MenuItemState FetchAndCheckout { get; } = new();

    public MenuItemState FetchAndMerge { get; } = new();

    public MenuItemState FetchAndRebase { get; } = new();

    public MenuItemState FetchAndCreateBranch { get; } = new();

    public MenuItemState FetchBranch { get; } = new();

    public MenuItemState CreateBranchInFolder { get; } = new();

    public MenuItemState DeleteAllBranches { get; } = new();

    public MenuItemState StashAll { get; } = new();

    public MenuItemState StashStaged { get; } = new();

    public MenuItemState ManageStashes { get; } = new();

    public MenuItemState OpenStash { get; } = new();

    public MenuItemState ApplyStash { get; } = new();

    public MenuItemState PopStash { get; } = new();

    public MenuItemState DropStash { get; } = new();

    public MenuItemState CreateWorktree { get; } = new();

    public MenuItemState PruneWorktrees { get; } = new();

    public MenuItemState ManageWorktrees { get; } = new();

    public MenuItemState OpenWorktree { get; } = new();

    public MenuItemState DeleteWorktree { get; } = new();

    public MenuItemState CopyWorktreePath { get; } = new();

    public MenuItemState ShowWorktreeInFolder { get; } = new();

    public MenuItemState Collapse { get; } = new();

    public MenuItemState Expand { get; } = new();

    public MenuItemState MoveUp { get; } = new();

    public MenuItemState MoveDown { get; } = new();

    public MenuItemState SortBy { get; } = new();

    public MenuItemState SortOrder { get; } = new();

    /// <summary>
    ///  Upstream's <c>AddUserScripts</c>: the grid's scripts, for a local branch the grid shows.
    /// </summary>
    public MenuItemState RunScript { get; } = new();

    /// <summary>
    ///  The nodes "Filter for selected", Expand and Collapse act on: the marked ones while there are several, else the
    ///  selected one. Upstream takes the marked ones always, so its menu opened from the keyboard acts on the node clicked
    ///  last, or offers nothing when none was; the new shell does not copy that.
    /// </summary>
    public static IReadOnlyList<RepoObjectNode> Targets(IReadOnlyList<RepoObjectNode> roots, RepoObjectNode? node)
        => RepoObjectSelection.Selected(roots) is { Count: > 1 } marked ? marked
            : node is not null ? [node] : [];

    /// <summary>
    ///  The state for <paramref name="input"/> as the menu opens; false when no item can run, so the menu does not open.
    /// </summary>
    public bool Refresh(RepoObjectMenuInput input)
    {
        RepoObjectNode? node = input.Node;
        bool open = input.IsOpen && node is not null;
        IReadOnlyList<RepoObjectNode> targets = open ? Targets(input.Roots, node) : [];

        // Upstream's hasSingleSelection: with several marked nodes only the items for all of them are offered.
        bool multiple = open && RepoObjectSelection.Selected(input.Roots).Count > 1;
        RepoObjectKind? kind = open && !multiple ? node!.Kind : null;
        bool isRef = kind is RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch or RepoObjectKind.Tag;
        bool isCurrent = node?.Branch is { IsCurrent: true };
        bool isTag = kind == RepoObjectKind.Tag;
        bool isRemoteBranch = kind == RepoObjectKind.RemoteBranch;

        // Upstream's copyContextMenuItem: the grid's selected commits, for a branch or a stash the grid shows.
        Copy.Show(kind is RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch or RepoObjectKind.Stash
                  && node?.IsHidden is false && input.SelectedRevisionCount > 0);
        Filter.Show(open && RepoObjectSelection.RefsFilter(targets) is not null);

        // Upstream's MenuItemsGenerator: the ref items with the texts of the ref's kind; the checked-out branch can only be
        // branched from and renamed.
        Checkout.Show(isRef, enabled: !isCurrent);
        Merge.Show(isRef, enabled: !isCurrent);
        Rebase.Show(isRef, enabled: !isCurrent);
        CreateBranch.Show(isRef);
        Reset.Show(isRef, enabled: !isCurrent);
        RenameBranch.Show(kind == RepoObjectKind.LocalBranch);
        Delete.Show(isRef, enabled: !isCurrent);
        DeleteRemoteTag.Show(isTag && input.RemoteCount > 0);
        if (isRef)
        {
            SetText(Checkout, isTag ? CheckoutTagText : isRemoteBranch ? CheckoutRemoteBranchText : CheckoutBranchText,
                isTag ? CheckoutTagTooltip : CheckoutBranchTooltip);
            SetText(Rebase, isTag ? RebaseOnTagText : isRemoteBranch ? RebaseOnRemoteBranchText : RebaseOnBranchText,
                isTag ? RebaseOnTagTooltip : RebaseOnBranchTooltip);
            SetText(Delete, isTag ? DeleteTagText : isRemoteBranch ? DeleteRemoteBranchText : DeleteBranchText,
                isTag ? DeleteTagTooltip : isRemoteBranch ? DeleteRemoteBranchTooltip : DeleteBranchTooltip);
            Merge.Tip = _translate(isTag ? MergeTagTooltip : MergeBranchTooltip);
            CreateBranch.Tip = _translate(isTag ? CreateFromTagTooltip : CreateFromBranchTooltip);
        }

        bool remotesRoot = kind == RepoObjectKind.RemotesRoot;
        ManageRemotes.Show(remotesRoot);
        FetchAllRemotes.Show(remotesRoot);
        PruneAllRemotes.Show(remotesRoot);

        bool remote = kind == RepoObjectKind.Remote;
        bool activeRemote = remote && !node!.IsInactive;
        ManageRemote.Show(remote);
        ActivateRemote.Show(remote && !activeRemote);
        ActivateAndFetchRemote.Show(remote && !activeRemote);
        DeactivateRemote.Show(activeRemote);
        FetchRemote.Show(activeRemote);
        PruneRemote.Show(activeRemote);
        OpenRemoteUrl.Show(remote, enabled: input.HasRemoteWebUrl);

        // Upstream's EnableSubmoduleContextMenu: Open for a project other than the shown one; Manage and Synchronize for the
        // shown one; Reset, Stash and Commit unless the shown repository is bare.
        SubmoduleProject? project = kind == RepoObjectKind.Submodule ? node?.Item as SubmoduleProject : null;
        bool bare = input.IsBareRepository;
        OpenSubmodule.Show(project is { IsCurrent: false });
        OpenSubmoduleNewInstance.Show(project is not null);
        ManageSubmodules.Show(project is { IsCurrent: true } && !bare);
        UpdateSubmodule.Show(project is not null);
        SynchronizeSubmodule.Show(project is { IsCurrent: true } && !bare);
        ResetSubmodule.Show(project is not null && !bare);
        StashSubmodule.Show(project is not null && !bare);
        CommitSubmodule.Show(project is not null && !bare);

        FetchAndCheckout.Show(isRemoteBranch);
        FetchAndMerge.Show(isRemoteBranch);
        FetchAndRebase.Show(isRemoteBranch);
        FetchAndCreateBranch.Show(isRemoteBranch);
        FetchBranch.Show(isRemoteBranch);

        bool branchFolder = kind == RepoObjectKind.Folder
                            && RepoObjectsTree.PathTo(input.Roots, node!).FirstOrDefault()?.Kind == RepoObjectKind.BranchesRoot;
        CreateBranchInFolder.Show(branchFolder);
        DeleteAllBranches.Show(branchFolder);

        bool stashesRoot = kind == RepoObjectKind.StashesRoot;
        StashAll.Show(stashesRoot);
        StashStaged.Show(stashesRoot);
        ManageStashes.Show(stashesRoot);
        bool stash = kind == RepoObjectKind.Stash;
        OpenStash.Show(stash);
        ApplyStash.Show(stash);
        PopStash.Show(stash);
        DropStash.Show(stash);

        bool worktreesRoot = kind == RepoObjectKind.WorktreesRoot;
        CreateWorktree.Show(worktreesRoot);
        PruneWorktrees.Show(worktreesRoot);
        ManageWorktrees.Show(worktreesRoot);
        bool worktree = kind == RepoObjectKind.Worktree;
        bool otherWorktree = worktree && node is { IsBold: false, Item: WorktreeInfo { IsDeleted: false } };
        OpenWorktree.Show(worktree, enabled: otherWorktree);
        DeleteWorktree.Show(worktree, enabled: otherWorktree);
        CopyWorktreePath.Show(worktree);
        ShowWorktreeInFolder.Show(worktree,
            enabled: node?.Item is WorktreeInfo { Path: var worktreePath } && _directoryExists(worktreePath));

        // Upstream's EnableExpandCollapseContextMenu (the menu's nodes with children: Expand for a collapsed one, Collapse
        // for an expanded one) and EnableMoveTreeUpDownContexMenu.
        RepoObjectNode[] parents = [.. targets.Where(target => target.Children.Count > 0)];
        Collapse.Show(parents.Length > 0, enabled: parents.Any(target => target.IsExpanded));
        Expand.Show(parents.Length > 0, enabled: parents.Any(target => !target.IsExpanded));
        bool root = open && !multiple && node!.IsRoot;
        int index = root ? input.Roots.ToList().IndexOf(node!) : -1;
        MoveUp.Show(root, enabled: index > 0);
        MoveDown.Show(root, enabled: index >= 0 && index < input.Roots.Count - 1);

        // Upstream's EnableSortContextMenu: the order only matters when the refs are not in git's order.
        SortBy.Show(isRef);
        SortOrder.Show(isRef, enabled: input.RefsSortBy != GitRefsSortBy.Default);

        RunScript.Show(kind == RepoObjectKind.LocalBranch && node?.IsHidden is false);

        // "Run script" can run only with scripts, which the view adds.
        return Items.Any(item => item.IsOffered);
    }

    private IEnumerable<MenuItemState> Items
        =>
        [
            Copy, Filter, Checkout, Merge, Rebase, CreateBranch, Reset, RenameBranch, Delete, DeleteRemoteTag,
            ManageRemotes, FetchAllRemotes, PruneAllRemotes, ManageRemote, ActivateRemote, ActivateAndFetchRemote,
            DeactivateRemote, FetchRemote, PruneRemote, OpenRemoteUrl, OpenSubmodule, OpenSubmoduleNewInstance,
            ManageSubmodules, UpdateSubmodule, SynchronizeSubmodule, ResetSubmodule, StashSubmodule, CommitSubmodule,
            FetchAndCheckout, FetchAndMerge, FetchAndRebase, FetchAndCreateBranch, FetchBranch, CreateBranchInFolder,
            DeleteAllBranches, StashAll, StashStaged, ManageStashes, OpenStash, ApplyStash, PopStash, DropStash,
            CreateWorktree, PruneWorktrees, ManageWorktrees, OpenWorktree, DeleteWorktree, CopyWorktreePath,
            ShowWorktreeInFolder, Collapse, Expand, MoveUp, MoveDown, SortBy, SortOrder,
        ];

    private void SetText(MenuItemState item, string text, string tip)
    {
        item.Text = _translate(text);
        item.Tip = _translate(tip);
    }
}
