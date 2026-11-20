using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GitCommands;
using GitCommands.Remotes;
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
    private readonly RepoObjectSearch _repoObjectSearch = new();
    private IReadOnlyList<RepoObjectNode> _repoObjects = [];

    // True while a refresh puts the selection back (upstream's IgnoreSelectionChangedEvent).
    private bool _restoringRepoObjectSelection;

    // Whether the click that selects a node held Ctrl (Cmd on macOS), which upstream's GoToRevision reads.
    private bool _toggleGridSelection;

    // Whether that click held Alt, which upstream's BaseBranchLeafNode.SelectRevision reads to go to the related branch.
    private bool _goToRelatedBranch;

    private RepoObjectNode? SelectedRepoObject => BranchTreeView.SelectedItem as RepoObjectNode;

    private void WireRepoObjectsTree()
    {
        BranchTreeView.SelectionChanged += (_, _) =>
        {
            UpdateBusyState();
            GoToSelectedRepoObject();
        };
        BranchTreeView.AddHandler(PointerPressedEvent, (_, e) =>
        {
            _toggleGridSelection = e.KeyModifiers.HasFlag(Hotkeys.CommandModifier);
            _goToRelatedBranch = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        }, RoutingStrategies.Tunnel);

        // Upstream's OnNodeClick: a click marks the node for the multi-selection, Ctrl (Cmd on macOS) toggles it, Shift
        // takes its descendants along.
        BranchTreeView.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.Source is Visual visual && visual.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext is
                    RepoObjectNode node)
            {
                RepoObjectSelection.Click(_repoObjects, node,
                    rightButton: e.GetCurrentPoint(BranchTreeView).Properties.IsRightButtonPressed,
                    multiple: e.KeyModifiers.HasFlag(Hotkeys.CommandModifier),
                    includingDescendants: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            }
        }, RoutingStrategies.Tunnel);
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

        LeftPanelSearchBox.GotFocus += (_, _) => LeftPanelSearchBox.ItemsSource = RepoObjectSearch.Candidates(_repoObjects);
        LeftPanelSearchBox.TextChanged += (_, _) => _repoObjectSearch.CriteriaChanged();
        LeftPanelSearchBox.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                SearchRepoObjects();
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    // Upstream's trees refill after each refresh; the expanded nodes, the selection and the keyboard focus stay.
    private void ShowRepoObjects()
    {
        string? selectedKey = SelectedRepoObject?.Key;
        bool hadFocus = BranchTreeView.IsKeyboardFocusWithin;
        IReadOnlyList<RepoObjectNode> roots = RepositoryPath is not { } path
            ? []
            : RepoObjectsTree.Build(
                new RepoObjects(_repository.Branches, _repository.Remotes, _repository.Tags, _repository.Stashes,
                    ShownSubmoduleStructure(path), _repository.Worktrees, _repository.AheadBehind, _repository.RemoteUrls, path,
                    _repository.InactiveRemotes, _repository.RelatedBranches),
                [.. _preferences.RepoObjectsTreeOrder.Where(_preferences.ShowsRepoObjectsTree)], UpstreamTranslation.ByText,
                RefSorting());
        RepoObjectsTree.KeepExpansion(_repoObjects, roots);
        RepoObjectSelection.Keep(_repoObjects, roots);
        _repoObjects = roots;
        _repoObjectSearch.Remap(roots);
        ShowHiddenRepoObjects();
        ShowMergedBranches();
        BranchTreeView.ItemsSource = roots;
        if (selectedKey is not null
            && roots.SelectMany(root => root.Descendants()).FirstOrDefault(node => node.Key == selectedKey) is { } selected)
        {
            _restoringRepoObjectSelection = true;
            try
            {
                BranchTreeView.SelectedItem = selected;
            }
            finally
            {
                _restoringRepoObjectSelection = false;
            }
        }

        if (hadFocus)
        {
            FocusSelectedRepoObject();
        }
    }

    // New items replace the focused node's item, which leaves the keyboard focus on nothing, so a key pressed after a refresh
    // (such as the one when the window is activated) was lost. Upstream's tree keeps its nodes and the focus: the selected
    // node's new item takes it, once the tree has made its items.
    private void FocusSelectedRepoObject()
    {
        if (SelectedRepoObject is not { } node)
        {
            return;
        }

        BranchTreeView.UpdateLayout();
        BranchTreeView.GetVisualDescendants().OfType<TreeViewItem>().FirstOrDefault(item => item.DataContext == node)?.Focus();
    }

    // Upstream's OnSelected of the branch, tag, stash and worktree nodes (GoToRevision): their commit is selected in the
    // grid, or added to or taken from its selection with Ctrl, with upstream's messages when the grid cannot show it.
    private void GoToSelectedRepoObject()
    {
        bool toggle = _toggleGridSelection;
        bool related = _goToRelatedBranch;
        _toggleGridSelection = false;
        _goToRelatedBranch = false;
        if (_restoringRepoObjectSelection || SelectedRepoObject is not { } node
                                          || RepoObjectsTree.RevisionOf(node, related) is not { } revision)
        {
            return;
        }

        Run(() => GoToRevisionAsync(revision, showNoRevisionMessage: true, toggleSelection: toggle));
    }

    // Upstream's DoSearch: the next match (RepoObjectSearch), shown and selected.
    private void SearchRepoObjects()
    {
        LeftPanelSearchBox.IsDropDownOpen = false;
        RepoObjectNode? node = _repoObjectSearch.Next(_repoObjects, LeftPanelSearchBox.Text ?? "", out bool focusSearchBox);
        if (focusSearchBox)
        {
            LeftPanelSearchBox.Focus();
        }

        if (node is null)
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
                Run(() => ShowStashWindowAsync(this, initialStash: ((StashInfo)node.Item!).Name));
                break;
            case RepoObjectKind.Worktree when !node.IsBold:
                OpenWorktree((WorktreeInfo)node.Item!);
                break;
            case RepoObjectKind.Submodule when node.Item is SubmoduleProject project:
                OpenSubmoduleOnDoubleClick(project);
                break;
            case RepoObjectKind.Remote:
                Run(() => ShowRemotesAsync(this, (string)node.Item!));
                break;
        }
    }

    // Upstream's RepoObjectsTree.ExecuteCommand: Delete and Rename act on the selected node (OnDelete, OnRename, which only
    // a local branch has), Search searches as the search button does.
    private void RunLeftPanelHotkey(LeftPanelCommand command, KeyModifiers modifiers)
    {
        switch (command)
        {
            // Upstream's MultiSelect and MultiSelectWithChildren click the selected node with the keys held. On macOS
            // MultiSelect's default has no Cmd (Cmd+Space is Spotlight's), so the command itself means "toggle" there.
            case LeftPanelCommand.MultiSelect:
            case LeftPanelCommand.MultiSelectWithChildren:
                if (SelectedRepoObject is { } selected)
                {
                    RepoObjectSelection.Click(_repoObjects, selected, rightButton: false,
                        multiple: OperatingSystem.IsMacOS() || modifiers.HasFlag(Hotkeys.CommandModifier),
                        includingDescendants: modifiers.HasFlag(KeyModifiers.Shift));
                }

                break;
            case LeftPanelCommand.Delete:
                DeleteSelectedRepoObject();
                break;
            case LeftPanelCommand.Rename:
                if (SelectedRepoObject is { Kind: RepoObjectKind.LocalBranch, Branch: { } branch })
                {
                    Run(() => RenameBranchAsync(branch.Name));
                }

                break;
            case LeftPanelCommand.Search:
                SearchRepoObjects();
                break;
        }
    }

    // The Delete key: upstream's OnDelete of a branch or tag node; a tag opens upstream's delete tag window
    // (TagNode.Delete).
    private void DeleteSelectedRepoObject()
    {
        switch (SelectedRepoObject)
        {
            case { Kind: RepoObjectKind.LocalBranch or RepoObjectKind.RemoteBranch }:
                DeleteSelectedBranch();
                break;
            case { Kind: RepoObjectKind.Tag, Item: string tag }:
                RunOnRepository(path => ShowDeleteTagAsync(path, tag));
                break;
        }
    }

    // Upstream's contextMenu_Opening: which items apply and can run is the toolbar view model's TreeMenu (bound in XAML);
    // here the copy and script submenus are filled and the separators between empty groups hidden. The menu does not open
    // without an enabled item.
    private bool ShowRepoObjectMenu()
    {
        RepoObjectNode? node = SelectedRepoObject;
        bool offered = Toolbar.TreeMenu.Refresh(RepoObjectMenuState());
        if (Toolbar.TreeMenu.Copy.IsVisible)
        {
            FillCopyMenu(TreeCopyMenuItem, SelectedRevisions, keep: null);
        }

        // Upstream's AddUserScripts for a local branch the grid shows: the scripts of the grid's menu, run on the grid's
        // selection.
        if (Toolbar.TreeMenu.RunScript.IsVisible)
        {
            ScriptMenus.Fill(BranchTreeContextMenu, TreeRunScriptMenuItem, script => script.AddToRevisionGridContextMenu,
                Toolbar.RunGridScriptCommand);
        }
        else
        {
            ScriptMenus.Remove(BranchTreeContextMenu, TreeRunScriptMenuItem);
        }

        ToggleSeparators();
        return offered || (TreeRunScriptMenuItem.IsVisible && TreeRunScriptMenuItem.IsEnabled);
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

    private void OnSelectedRemoteName(Action<string> action)
    {
        if (SelectedRepoObject is { Kind: RepoObjectKind.Remote, Item: string name })
        {
            action(name);
        }
    }

    // Upstream's RemoteRepoNode.Enable and Disable: the remote's section moves between "remote" and "-remote" in
    // .git/config, so git no longer sees an inactive remote; an activated remote can be fetched at once.
    private void SetRemoteActive(string name, bool active, bool fetch = false)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        Run(async () =>
        {
            await Task.Run(() => new ConfigFileRemoteSettingsManager(() => GitModules.Open(path))
                .ToggleRemoteState(name, disabled: !active));
            RefreshRepository();
            if (fetch)
            {
                Fetch(remote: name);
            }
        });
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
    // The nodes "Filter for selected", Expand and Collapse act on (RepoObjectMenuViewModel.Targets).
    private IReadOnlyList<RepoObjectNode> RepoObjectMenuTargets()
        => RepoObjectMenuViewModel.Targets(_repoObjects, SelectedRepoObject);

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

    // Upstream's WorktreeNode.OpenWorktree: upstream's WorktreeSwitch, with its question.
    private void OpenWorktree(WorktreeInfo worktree) => Run(() => WorktreeFlow().SwitchAsync(this, worktree.Path));

    // Upstream's FormDeleteBranch: work not merged into HEAD (or a detached HEAD) is deleted only after upstream's
    // question, unless the user turned it off; the branches are then deleted with -D.
    private async Task<bool> DeleteLocalBranchesAsync(string path, IReadOnlyList<string> branches, Window? owner = null)
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

        if (unmerged && !await ConfirmWindow.AskAsync(owner ?? this, _preferences, Confirmation.DeleteUnmergedBranch,
                Confirmations.DeleteUnmergedBranchQuestion, "Delete", "Delete Confirmation"))
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

    // Upstream's StartRenameDialog: FormRenameBranch on the branch.
    private async Task RenameBranchAsync(string branch)
    {
        if (RepositoryPath is not { } path)
        {
            return;
        }

        await new RenameBranchWindow(branch, _preferences, arguments => RunGitWithOutputAsync(path, arguments))
            .ShowDialog<bool>(this);
    }
}
