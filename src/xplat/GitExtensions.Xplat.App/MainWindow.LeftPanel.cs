using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's left panel (<c>RepoObjectsTree</c>): one tree of branches, remotes, worktrees, tags, submodules and stashes
///  with upstream's images, the toolstrip that shows or hides each tree, the search box, and the context menu of
///  <c>RepoObjectsTree.ContextActions</c>, which shows the items that apply to the node it opens on.
/// </summary>
public partial class MainWindow
{
    // Upstream's texts that depend on the kind of ref (BranchMenuItemsStrings, RemoteBranchMenuItemsStrings and
    // TagMenuItemsStrings).
    private const string CheckoutBranchText = "Chec_kout branch...";
    private const string CheckoutRemoteBranchText = "Chec_kout remote branch...";
    private const string CheckoutTagText = "Chec_kout tag revision...";
    private const string RebaseOnBranchText = "_Rebase current branch on this branch...";
    private const string RebaseOnRemoteBranchText = "_Rebase current branch on this remote branch...";
    private const string RebaseOnTagText = "_Rebase current branch on this tag revision...";
    private const string DeleteBranchText = "_Delete branch...";
    private const string DeleteRemoteBranchText = "_Delete remote branch...";
    private const string DeleteTagText = "_Delete tag...";
    private const string CheckoutBranchTooltip = "Checkout this branch";
    private const string CheckoutTagTooltip = "Checkout this tag revision";
    private const string MergeBranchTooltip = "Merge this branch into current branch";
    private const string MergeTagTooltip = "Merge this tag into current branch";
    private const string CreateFromBranchTooltip = "Create a local branch from this branch";
    private const string CreateFromTagTooltip = "Create a local branch from this tag";
    private const string RebaseOnBranchTooltip = "Rebase current branch on this branch";
    private const string RebaseOnTagTooltip = "Rebase current branch on this tag";
    private const string DeleteBranchTooltip = "Delete the branch, which must be fully merged in its upstream branch or in HEAD";
    private const string DeleteRemoteBranchTooltip = "Delete the branch from the remote";
    private const string DeleteTagTooltip = "Delete this tag";

    // The commit context menu's reset texts, offered by the left panel's "Reset current branch to here...".
    private const string ResetSoftText = "Soft: keep index and working tree";
    private const string ResetMixedText = "Mixed: keep working tree, reset index";
    private const string ResetHardText = "Hard: discard all changes...";

    private IReadOnlyList<RepoObjectNode> _repoObjects = [];

    private RepoObjectNode? SelectedRepoObject => BranchTreeView.SelectedItem as RepoObjectNode;

    private void WireRepoObjectsTree()
    {
        BranchTreeView.SelectionChanged += (_, _) => UpdateBusyState();
        BranchTreeView.DoubleTapped += (_, e) =>
        {
            if (e.Source is Visual visual && visual.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is RepoObjectNode node
                && node == SelectedRepoObject)
            {
                OpenRepoObject(node);
            }
        };

        // As upstream's tree, a right click selects the node the menu then opens on.
        BranchTreeView.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(BranchTreeView).Properties.IsRightButtonPressed && e.Source is Visual visual
                && visual.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is RepoObjectNode node)
            {
                BranchTreeView.SelectedItem = node;
            }
        }, RoutingStrategies.Tunnel);
        BranchTreeContextMenu.Opening += (_, e) => e.Cancel = !ShowRepoObjectMenu();

        CollapseAllButton.Click += (_, _) =>
        {
            foreach (RepoObjectNode node in _repoObjects.SelectMany(root => root.Descendants()))
            {
                node.IsExpanded = false;
            }
        };
        foreach ((ToolToggleButton button, RepoObjectKind root) in TreeToggles())
        {
            button.IsChecked = _preferences.ShowsRepoObjectsTree(root);
            button.Click += (_, _) =>
            {
                _preferences.ShowRepoObjectsTree(root, !_preferences.ShowsRepoObjectsTree(root));
                button.IsChecked = _preferences.ShowsRepoObjectsTree(root);
                ShowRepoObjects();
            };
        }

        LeftPanelSearchBox.GotFocus += (_, _) => LeftPanelSearchBox.ItemsSource = _repoObjects
            .SelectMany(root => root.Descendants()).Where(node => !node.IsRoot).Select(node => node.Text).Distinct().ToList();
        LeftPanelSearchBox.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                SearchRepoObjects();
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        LeftPanelSearchButton.Click += (_, _) => SearchRepoObjects();

        WireRepoObjectMenu();
    }

    private (ToolToggleButton Button, RepoObjectKind Root)[] TreeToggles() =>
    [
        (ShowBranchesTreeButton, RepoObjectKind.BranchesRoot), (ShowRemotesTreeButton, RepoObjectKind.RemotesRoot),
        (ShowWorktreesTreeButton, RepoObjectKind.WorktreesRoot), (ShowTagsTreeButton, RepoObjectKind.TagsRoot),
        (ShowSubmodulesTreeButton, RepoObjectKind.SubmodulesRoot), (ShowStashesTreeButton, RepoObjectKind.StashesRoot),
    ];

    // Upstream's trees refill after each refresh; the expanded nodes and the selection stay.
    private void ShowRepoObjects()
    {
        string? selectedKey = SelectedRepoObject?.Key;
        IReadOnlyList<RepoObjectNode> roots = RepositoryPath is not { } path
            ? []
            : RepoObjectsTree.Build(
                new RepoObjects(_repository.Branches, _repository.Remotes, _repository.Tags, _repository.Stashes,
                    _repository.Submodules, _repository.Worktrees, _repository.AheadBehind, _repository.RemoteUrls, path),
                [.. _preferences.RepoObjectsTreeOrder.Where(_preferences.ShowsRepoObjectsTree)], UpstreamTranslation.ByText,
                RefSorting());
        RepoObjectsTree.KeepExpansion(_repoObjects, roots);
        _repoObjects = roots;
        BranchTreeView.ItemsSource = roots;
        if (selectedKey is not null
            && roots.SelectMany(root => root.Descendants()).FirstOrDefault(node => node.Key == selectedKey) is { } selected)
        {
            BranchTreeView.SelectedItem = selected;
        }
    }

    // Upstream's DoSearch: the next node whose text matches, shown and selected.
    private void SearchRepoObjects()
    {
        if (RepoObjectsTree.FindNext(_repoObjects, LeftPanelSearchBox.Text ?? "", SelectedRepoObject) is not { } node)
        {
            return;
        }

        foreach (RepoObjectNode parent in RepoObjectsTree.PathTo(_repoObjects, node).SkipLast(1))
        {
            parent.IsExpanded = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            BranchTreeView.SelectedItem = node;
            BranchTreeView.ScrollIntoView(node);
        }, DispatcherPriority.Loaded);
    }

    // Upstream's OnDoubleClick of each node.
    private void OpenRepoObject(RepoObjectNode node)
    {
        switch (node.Kind)
        {
            case RepoObjectKind.LocalBranch:
            case RepoObjectKind.RemoteBranch:
                CheckoutSelectedBranch();
                break;
            case RepoObjectKind.Tag:
                Run(() => PromptCreateBranchAsync((string)node.Item!));
                break;
            case RepoObjectKind.Stash:
                ShowStashDiff((StashInfo)node.Item!);
                break;
            case RepoObjectKind.Worktree when !node.IsBold:
                OpenWorktree((WorktreeInfo)node.Item!);
                break;
            case RepoObjectKind.Submodule:
                OpenSubmodule((SubmoduleInfo)node.Item!);
                break;
        }
    }

    // The Delete key: upstream's OnDelete of a branch or tag node.
    private void DeleteSelectedRepoObject()
    {
        switch (SelectedRepoObject)
        {
            case { Kind: RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch }:
                DeleteSelectedBranch();
                break;
            case { Kind: RepoObjectKind.Tag, Item: string tag }:
                RunOnRepository(path => _actions.DeleteTagAsync(path, tag));
                break;
        }
    }

    // Upstream's contextMenu_Opening: the items that apply to the selected node are shown, enabled when they can run now,
    // and the separators between empty groups are hidden. The menu does not open without an enabled item.
    private bool ShowRepoObjectMenu()
    {
        RepoObjectNode? node = SelectedRepoObject;
        bool open = RepositoryPath is not null && !IsBusy && node is not null;
        RepoObjectKind? kind = open ? node!.Kind : null;
        bool isRef = kind is RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch or RepoObjectKind.Tag;
        bool isCurrent = node?.Branch is { IsCurrent: true };
        bool isTag = kind == RepoObjectKind.Tag;
        bool isRemoteBranch = kind == RepoObjectKind.RemoteBranch;

        foreach (MenuItem item in BranchTreeContextMenu.Items.OfType<MenuItem>())
        {
            item.IsVisible = false;
            item.IsEnabled = true;
        }

        Show(TreeFilterMenuItem, isRef);

        // Upstream's MenuItemsGenerator: the ref items with the texts of the ref's kind; the checked-out branch can only be
        // branched from and renamed.
        Show(TreeCheckoutMenuItem, isRef, enabled: !isCurrent);
        Show(TreeMergeMenuItem, isRef, enabled: !isCurrent);
        Show(TreeRebaseMenuItem, isRef, enabled: !isCurrent);
        Show(TreeCreateBranchMenuItem, isRef);
        Show(TreeResetMenuItem, isRef, enabled: !isCurrent);
        Show(TreeRenameBranchMenuItem, kind == RepoObjectKind.LocalBranch);
        Show(TreeDeleteMenuItem, isRef, enabled: !isCurrent);
        Show(TreeDeleteRemoteTagMenuItem, isTag && _repository.Remotes.Count > 0);
        if (isRef)
        {
            SetText(TreeCheckoutMenuItem, isTag ? CheckoutTagText : isRemoteBranch ? CheckoutRemoteBranchText : CheckoutBranchText,
                isTag ? CheckoutTagTooltip : CheckoutBranchTooltip);
            SetText(TreeRebaseMenuItem, isTag ? RebaseOnTagText : isRemoteBranch ? RebaseOnRemoteBranchText : RebaseOnBranchText,
                isTag ? RebaseOnTagTooltip : RebaseOnBranchTooltip);
            SetText(TreeDeleteMenuItem, isTag ? DeleteTagText : isRemoteBranch ? DeleteRemoteBranchText : DeleteBranchText,
                isTag ? DeleteTagTooltip : isRemoteBranch ? DeleteRemoteBranchTooltip : DeleteBranchTooltip);
            ToolTip.SetTip(TreeMergeMenuItem, UpstreamTranslation.ByText(isTag ? MergeTagTooltip : MergeBranchTooltip));
            ToolTip.SetTip(TreeCreateBranchMenuItem,
                UpstreamTranslation.ByText(isTag ? CreateFromTagTooltip : CreateFromBranchTooltip));
        }

        bool remotesRoot = kind == RepoObjectKind.RemotesRoot;
        Show(TreeAddRemoteMenuItem, remotesRoot);
        Show(TreeFetchAllRemotesMenuItem, remotesRoot);
        Show(TreePruneAllRemotesMenuItem, remotesRoot);

        bool remote = kind == RepoObjectKind.Remote;
        Show(TreeRenameRemoteMenuItem, remote);
        Show(TreeSetRemoteUrlMenuItem, remote);
        Show(TreeRemoveRemoteMenuItem, remote);
        Show(TreeFetchRemoteMenuItem, remote);
        Show(TreePruneRemoteMenuItem, remote);
        Show(TreeOpenRemoteUrlMenuItem, remote, enabled: RemoteWebUrl(node) is not null);

        bool submodule = kind == RepoObjectKind.Submodule;
        bool initialized = node?.Item is SubmoduleInfo { IsInitialized: true };
        Show(TreeOpenSubmoduleMenuItem, submodule, enabled: initialized);
        Show(TreeOpenSubmoduleNewInstanceMenuItem, submodule, enabled: initialized);
        Show(TreeUpdateSubmoduleMenuItem, submodule);
        Show(TreeSynchronizeSubmoduleMenuItem, submodule);

        Show(TreeFetchAndCheckoutMenuItem, isRemoteBranch);
        Show(TreeFetchAndMergeMenuItem, isRemoteBranch);
        Show(TreeFetchAndRebaseMenuItem, isRemoteBranch);
        Show(TreeFetchAndCreateBranchMenuItem, isRemoteBranch);
        Show(TreeFetchBranchMenuItem, isRemoteBranch);

        bool branchFolder = kind == RepoObjectKind.Folder && RootOf(node!)?.Kind == RepoObjectKind.BranchesRoot;
        Show(TreeCreateBranchInFolderMenuItem, branchFolder);
        Show(TreeDeleteAllBranchesMenuItem, branchFolder);

        Show(TreeStashAllMenuItem, kind == RepoObjectKind.StashesRoot);
        Show(TreeStashStagedMenuItem, kind == RepoObjectKind.StashesRoot);
        bool stash = kind == RepoObjectKind.Stash;
        Show(TreeOpenStashMenuItem, stash);
        Show(TreeApplyStashMenuItem, stash);
        Show(TreePopStashMenuItem, stash);
        Show(TreeDropStashMenuItem, stash);

        Show(TreeCreateWorktreeMenuItem, kind == RepoObjectKind.WorktreesRoot);
        Show(TreePruneWorktreesMenuItem, kind == RepoObjectKind.WorktreesRoot);
        Show(TreeManageWorktreesMenuItem, kind == RepoObjectKind.WorktreesRoot);
        bool worktree = kind == RepoObjectKind.Worktree;
        bool otherWorktree = worktree && node is { IsBold: false, Item: WorktreeInfo { IsDeleted: false } };
        Show(TreeOpenWorktreeMenuItem, worktree, enabled: otherWorktree);
        Show(TreeDeleteWorktreeMenuItem, worktree, enabled: otherWorktree);
        Show(TreeCopyWorktreePathMenuItem, worktree);
        Show(TreeShowWorktreeInFolderMenuItem, worktree,
            enabled: node?.Item is WorktreeInfo { Path: var worktreePath } && Directory.Exists(worktreePath));

        // Upstream's EnableExpandCollapseContextMenu and EnableMoveTreeUpDownContexMenu.
        bool parent = open && node!.Children.Count > 0;
        Show(TreeCollapseMenuItem, parent, enabled: node?.IsExpanded == true);
        Show(TreeExpandMenuItem, parent, enabled: node?.Descendants().Any(child => child.Children.Count > 0 && !child.IsExpanded) == true);
        bool root = open && node!.IsRoot;
        int index = root ? _repoObjects.ToList().IndexOf(node!) : -1;
        Show(TreeMoveUpMenuItem, root, enabled: index > 0);
        Show(TreeMoveDownMenuItem, root, enabled: index >= 0 && index < _repoObjects.Count - 1);

        // Upstream's EnableSortContextMenu: the order only matters when the refs are not in git's order.
        Show(SortByMenuItem, isRef);
        Show(SortOrderMenuItem, isRef, enabled: _preferences.RefsSortBy != GitRefsSortBy.Default);

        ToggleSeparators();
        return BranchTreeContextMenu.Items.OfType<MenuItem>().Any(item => item.IsVisible && item.IsEnabled);

        static void Show(MenuItem item, bool visible, bool enabled = true)
        {
            item.IsVisible = visible;
            item.IsEnabled = visible && enabled;
        }

        static void SetText(MenuItem item, string text, string tooltip)
        {
            item.Header = UpstreamTranslation.ByText(text);
            ToolTip.SetTip(item, UpstreamTranslation.ByText(tooltip));
        }
    }

    // Upstream's ToggleSeparators: a separator shows only between two groups that both have a visible item.
    private void ToggleSeparators()
    {
        bool itemBefore = false;
        Separator? pending = null;
        foreach (Control control in BranchTreeContextMenu.Items.OfType<Control>())
        {
            if (control is Separator separator)
            {
                separator.IsVisible = false;
                if (itemBefore)
                {
                    pending = separator;
                }

                continue;
            }

            if (control.IsVisible)
            {
                pending?.IsVisible = true;
                pending = null;
                itemBefore = true;
            }
        }
    }

    // Upstream's RegisterContextActions, with the new shell's actions.
    private void WireRepoObjectMenu()
    {
        TreeFilterMenuItem.Click += (_, _) =>
        {
            if (RefName(SelectedRepoObject) is { } name)
            {
                _preferences.ShowCurrentBranchOnly = false;
                ApplyFilter(_commits.Filter with { CurrentBranchOnly = false, ShowFilteredBranches = true, BranchFilter = name });
            }
        };

        TreeCheckoutMenuItem.Click += (_, _) => OnSelectedRef(CheckoutSelectedBranch,
            tag => RunOnRepository(path => WithScriptsAsync(path, ScriptEvent.BeforeCheckout, ScriptEvent.AfterCheckout,
                () => CheckoutLocalBranchAsync(path, tag), PluginEvent.CheckoutRevision)));
        TreeMergeMenuItem.Click += (_, _) => OnSelectedRef(MergeSelectedBranch,
            tag => RunOnRepository(path => WithScriptsAsync(path, ScriptEvent.BeforeMerge, ScriptEvent.AfterMerge,
                () => _actions.MergeAsync(path, tag))));
        TreeRebaseMenuItem.Click += (_, _) => OnSelectedRef(RebaseOnSelectedBranch,
            tag => RunOnRepository(path => _actions.RebaseAsync(path, tag)));
        TreeCreateBranchMenuItem.Click += (_, _) =>
        {
            if (RefName(SelectedRepoObject) is { } name)
            {
                Run(() => PromptCreateBranchAsync(name));
            }
        };
        TreeResetMenuItem.Click += (_, _) =>
        {
            if (RefName(SelectedRepoObject) is { } name)
            {
                Run(() => ResetCurrentBranchToAsync(name));
            }
        };
        TreeRenameBranchMenuItem.Click += (_, _) =>
        {
            if (SelectedBranch is { IsRemote: false } branch)
            {
                Run(() => RenameBranchAsync(branch.Name));
            }
        };
        TreeDeleteMenuItem.Click += (_, _) => DeleteSelectedRepoObject();
        TreeDeleteRemoteTagMenuItem.Click += (_, _) =>
        {
            if (SelectedRepoObject is { Kind: RepoObjectKind.Tag, Item: string tag })
            {
                RunOnRepository(path => DeleteRemoteTagAfterConfirmAsync(path, tag));
            }
        };

        TreeAddRemoteMenuItem.Click += (_, _) => Run(AddRemoteAsync);
        TreeFetchAllRemotesMenuItem.Click += (_, _) => Fetch(remote: "--all", prune: false);
        TreePruneAllRemotesMenuItem.Click += (_, _) => Fetch(remote: "--all", prune: true);
        TreeRenameRemoteMenuItem.Click += (_, _) => OnSelectedRemote(RenameRemoteAsync);
        TreeSetRemoteUrlMenuItem.Click += (_, _) => OnSelectedRemote(SetRemoteUrlAsync);
        TreeRemoveRemoteMenuItem.Click += (_, _) => OnSelectedRemote(RemoveRemoteAfterConfirmAsync);
        TreeFetchRemoteMenuItem.Click += (_, _) => OnSelectedRemoteName(name => Fetch(remote: name, prune: false));
        TreePruneRemoteMenuItem.Click += (_, _) => OnSelectedRemoteName(name => Fetch(remote: name, prune: true));
        TreeOpenRemoteUrlMenuItem.Click += (_, _) =>
        {
            if (RemoteWebUrl(SelectedRepoObject) is { } url)
            {
                Run(() => Launcher.LaunchUriAsync(url));
            }
        };

        TreeOpenSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleInfo>(OpenSubmodule);
        TreeOpenSubmoduleNewInstanceMenuItem.Click += (_, _) => OnSelected<SubmoduleInfo>(submodule =>
            RunOnRepositoryFolder(path =>
            {
                string submodulePath = Path.GetFullPath(Path.Combine(path, submodule.Path));
                _launcher.Start(Environment.ProcessPath!, [submodulePath], submodulePath);
            }));
        TreeUpdateSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleInfo>(submodule =>
            RunOnRepository(path => UpdateSubmodulesAsync(path, submodule.Path)));
        TreeSynchronizeSubmoduleMenuItem.Click += (_, _) => OnSelected<SubmoduleInfo>(submodule =>
            RunOnRepository(path => _actions.SyncSubmodulesAsync(path, submodule.Path)));

        // Upstream's RemoteBranchNode actions; the new shell fetches the whole remote.
        TreeFetchAndCheckoutMenuItem.Click += (_, _) => OnSelectedRemoteBranch((path, remote, _, branch) =>
            FetchThenAsync(path, remote, () => _actions.CheckoutRemoteAsync(path, branch.Name)));
        TreeFetchAndMergeMenuItem.Click += (_, _) => OnSelectedRemoteBranch((path, remote, name, _) =>
            _actions.PullAsync(path, remote, name, rebase: false));
        TreeFetchAndRebaseMenuItem.Click += (_, _) => OnSelectedRemoteBranch((path, remote, name, _) =>
            _actions.PullAsync(path, remote, name, rebase: true));
        TreeFetchAndCreateBranchMenuItem.Click += (_, _) => OnSelectedRemoteBranch((path, remote, _, branch) =>
            FetchThenAsync(path, remote, async () =>
            {
                await PromptCreateBranchAsync(branch.Name);
                return true;
            }));
        TreeFetchBranchMenuItem.Click += (_, _) => OnSelectedRemoteBranch((path, remote, _, _) =>
            _actions.FetchAsync(path, remote, prune: false));

        TreeCreateBranchInFolderMenuItem.Click += (_, _) =>
        {
            if (SelectedRepoObject is { Kind: RepoObjectKind.Folder, Item: string folder })
            {
                Run(() => PromptCreateBranchInFolderAsync(folder));
            }
        };
        TreeDeleteAllBranchesMenuItem.Click += (_, _) =>
        {
            if (SelectedRepoObject is { Kind: RepoObjectKind.Folder } folder)
            {
                List<string> branches =
                [
                    .. folder.Descendants().Select(node => node.Branch)
                        .OfType<BranchInfo>().Where(branch => !branch.IsCurrent).Select(branch => branch.Name)
                ];
                RunOnRepository(path => DeleteLocalBranchesAsync(path, branches));
            }
        };

        TreeStashAllMenuItem.Click += (_, _) => Run(StashAsync);
        TreeStashStagedMenuItem.Click += (_, _) => RunOnRepository(path => _actions.StashStagedAsync(path));
        TreeOpenStashMenuItem.Click += (_, _) => OnSelected<StashInfo>(ShowStashDiff);
        TreeApplyStashMenuItem.Click += (_, _) => OnSelected<StashInfo>(stash =>
            RunOnRepository(path => _actions.ApplyStashAsync(path, stash.Name)));
        TreePopStashMenuItem.Click += (_, _) => OnSelected<StashInfo>(stash =>
            RunOnRepository(path => _actions.PopStashAsync(path, stash.Name)));
        TreeDropStashMenuItem.Click += (_, _) => OnSelected<StashInfo>(stash =>
            RunOnRepository(async path =>
                await ConfirmAsync(Confirmation.StashDrop, Confirmations.StashDropQuestion, "Drop", "Drop Stash Confirmation")
                && await _actions.DropStashAsync(path, stash.Name)));

        TreeCreateWorktreeMenuItem.Click += (_, _) => Run(ShowWorktreesAsync);
        TreePruneWorktreesMenuItem.Click += (_, _) => RunOnRepository(path => _actions.PruneWorktreesAsync(path));
        TreeManageWorktreesMenuItem.Click += (_, _) => Run(ShowWorktreesAsync);
        TreeOpenWorktreeMenuItem.Click += (_, _) => OnSelected<WorktreeInfo>(OpenWorktree);
        TreeDeleteWorktreeMenuItem.Click += (_, _) => OnSelected<WorktreeInfo>(worktree =>
            RunOnRepository(async path =>
                await new ConfirmWindow($"Delete the worktree at {worktree.Path}?", "Delete").ShowDialog<bool>(this)
                && await _actions.RemoveWorktreeAsync(path, worktree.Path, force: false)));
        TreeCopyWorktreePathMenuItem.Click += (_, _) => OnSelected<WorktreeInfo>(worktree => Run(async () =>
        {
            if (GetTopLevel(this)?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(worktree.Path);
            }
        }));
        TreeShowWorktreeInFolderMenuItem.Click += (_, _) => OnSelected<WorktreeInfo>(worktree =>
            RunOnRepositoryFolder(_ => _fileManager.OpenFolder(worktree.Path)));

        TreeCollapseMenuItem.Click += (_, _) => SelectedRepoObject?.IsExpanded = false;
        TreeExpandMenuItem.Click += (_, _) =>
        {
            foreach (RepoObjectNode node in SelectedRepoObject?.Descendants() ?? [])
            {
                node.IsExpanded = true;
            }
        };
        TreeMoveUpMenuItem.Click += (_, _) => MoveSelectedTree(up: true);
        TreeMoveDownMenuItem.Click += (_, _) => MoveSelectedTree(up: false);
    }

    // Upstream's ReorderTreeNode: the tree swaps places with the shown tree above or below it, and the order is kept.
    private void MoveSelectedTree(bool up)
    {
        if (SelectedRepoObject is not { IsRoot: true } root)
        {
            return;
        }

        int index = _repoObjects.ToList().IndexOf(root);
        int other = up ? index - 1 : index + 1;
        if (index < 0 || other < 0 || other >= _repoObjects.Count)
        {
            return;
        }

        List<RepoObjectKind> order = [.. _preferences.RepoObjectsTreeOrder];
        int from = order.IndexOf(root.Kind);
        int to = order.IndexOf(_repoObjects[other].Kind);
        (order[from], order[to]) = (order[to], order[from]);
        _preferences.RepoObjectsTreeOrder = order;
        ShowRepoObjects();
    }

    // A tag acts as a revision; branches go through the branch actions, which skip the checked-out branch.
    private void OnSelectedRef(Action onBranch, Action<string> onTag)
    {
        switch (SelectedRepoObject)
        {
            case { Kind: RepoObjectKind.Tag, Item: string tag }:
                onTag(tag);
                break;
            case { Branch: not null }:
                onBranch();
                break;
        }
    }

    private void OnSelected<T>(Action<T> action)
        where T : class
    {
        if (SelectedRepoObject?.Item is T item)
        {
            action(item);
        }
    }

    private void OnSelectedRemote(Func<string, string, Task<bool>> action)
        => OnSelectedRemoteName(name => RunOnRepository(path => action(path, name)));

    private void OnSelectedRemoteName(Action<string> action)
    {
        if (SelectedRepoObject is { Kind: RepoObjectKind.Remote, Item: string name })
        {
            action(name);
        }
    }

    // A remote branch's remote and its name there ("origin/feature/x": "origin" and "feature/x").
    private void OnSelectedRemoteBranch(Func<string, string, string, BranchInfo, Task<bool>> action)
    {
        if (SelectedBranch is { IsRemote: true } branch && branch.Name.IndexOf('/') is var slash and > 0)
        {
            RunOnRepository(path => action(path, branch.Name[..slash], branch.Name[(slash + 1)..], branch));
        }
    }

    private async Task<bool> FetchThenAsync(string path, string remote, Func<Task<bool>> then)
        => await _actions.FetchAsync(path, remote, prune: false) && await then();

    // Upstream's "Filter for selected" path of a ref: the branch name with its remote, or the tag name.
    private static string? RefName(RepoObjectNode? node)
        => node switch
        {
            { Branch: { } branch } => branch.Name,
            { Kind: RepoObjectKind.Tag, Item: string tag } => tag,
            _ => null,
        };

    // Upstream's RemoteRepoNode.IsRemoteUrlUsingHttp: only a web address opens in the browser.
    private Uri? RemoteWebUrl(RepoObjectNode? node)
        => node is { Kind: RepoObjectKind.Remote, Item: string name }
           && _repository.RemoteUrls.TryGetValue(name, out string? url)
           && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
            ? uri
            : null;

    private static RepoObjectNode? RootOf(RepoObjectNode node, IReadOnlyList<RepoObjectNode> roots)
        => RepoObjectsTree.PathTo(roots, node).FirstOrDefault();

    private RepoObjectNode? RootOf(RepoObjectNode node) => RootOf(node, _repoObjects);

    // The whole stash, not one file: a stash is a commit that git shows as a diff (upstream opens it in its stash window).
    private void ShowStashDiff(StashInfo stash)
    {
        if (RepositoryPath is { } path)
        {
            new DiffWindow(path, stash.Name, filePath: null, staged: false).Show(this);
        }
    }

    // As upstream's left panel, a submodule opens as a repository of its own in the browse window.
    private void OpenSubmodule(SubmoduleInfo submodule)
    {
        if (RepositoryPath is { } path && submodule.IsInitialized)
        {
            string submodulePath = Path.GetFullPath(Path.Combine(path, submodule.Path));
            Run(() => OpenRepositoryAsync(submodulePath));
        }
    }

    private void OpenWorktree(WorktreeInfo worktree) => Run(() => OpenRepositoryAsync(worktree.Path));

    // Upstream's FormDeleteBranch: work not merged into HEAD (or a detached HEAD) is deleted only after upstream's
    // question, unless the user turned it off; the branches are then deleted with -D.
    private async Task<bool> DeleteLocalBranchesAsync(string path, IReadOnlyList<string> branches)
    {
        if (branches.Count == 0)
        {
            return false;
        }

        bool unmerged = _repository.CurrentBranch.Length == 0;
        foreach (string branch in branches)
        {
            unmerged = unmerged || !await GitRepositoryService.IsMergedIntoHeadAsync(path, branch);
        }

        if (unmerged && !await ConfirmAsync(Confirmation.DeleteUnmergedBranch, Confirmations.DeleteUnmergedBranchQuestion,
                "Delete", "Delete Confirmation"))
        {
            return false;
        }

        bool deleted = true;
        foreach (string branch in branches)
        {
            deleted &= await _actions.DeleteBranchAsync(path, branch, force: true);
        }

        return deleted;
    }

    private async Task RenameBranchAsync(string branch)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        PromptResult? result = await new PromptWindow("Rename branch", $"New name of the branch {branch}.", "Rename",
                initialValue: branch)
            .ShowDialog<PromptResult?>(this);
        if (result is not null && NormaliseBranchName(result.Value) is var name && name != branch)
        {
            await _actions.RenameBranchAsync(path, branch, name);
        }
    }

    // Upstream's BranchPathNode.CreateBranch: the create branch dialog with the folder's path as the name's start.
    private async Task PromptCreateBranchInFolderAsync(string folder)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        PromptResult? result = await new PromptWindow("Create branch",
                "Name of the new branch at HEAD. It is checked out after it is created.", "Create",
                initialValue: $"{folder}/")
            .ShowDialog<PromptResult?>(this);
        if (result is not null)
        {
            await _actions.CreateBranchAsync(path, NormaliseBranchName(result.Value), checkout: true);
        }
    }

    // Stands in for upstream's FormRemotes, which the new shell does not have yet: the name and URL of a new remote.
    private async Task AddRemoteAsync()
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        PromptResult? result = await new PromptWindow("Add remote", "Name of the new remote.", "Add",
                secondPlaceholder: "URL or path")
            .ShowDialog<PromptResult?>(this);
        if (result is not null)
        {
            await _actions.AddRemoteAsync(path, result.Value, result.SecondValue);
        }
    }

    // Stands in for upstream's FormResetCurrentBranch: soft, mixed or hard, the hard reset after a question.
    private async Task ResetCurrentBranchToAsync(string target)
    {
        if (RepositoryPath is not { } path
            || await new ChooseWindow([ResetSoftText, ResetMixedText, ResetHardText]).ShowDialog<string?>(this) is not { } choice)
        {
            return;
        }

        ResetMode mode = choice == ResetSoftText ? ResetMode.Soft : choice == ResetMixedText ? ResetMode.Mixed : ResetMode.Hard;
        if (mode == ResetMode.Hard
            && !await new ConfirmWindow($"Reset the branch to {target} and discard all local changes? This cannot be undone.",
                "Reset").ShowDialog<bool>(this))
        {
            return;
        }

        await _actions.ResetAsync(path, target, mode);
    }
}
