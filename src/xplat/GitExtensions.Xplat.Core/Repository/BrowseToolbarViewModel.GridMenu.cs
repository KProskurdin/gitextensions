using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Scripts;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The grid menu's ref submenus as entries (upstream's <c>AddBranchMenuItem</c> for each ref of
///  <see cref="GridRefMenus"/>); a separator stands for a null ref.
/// </summary>
/// <param name="SelectInLeftPanel">Empty for a single ref, which the item itself selects.</param>
public sealed record GridRefMenuEntries(
    IReadOnlyList<MenuEntry> Checkout,
    IReadOnlyList<MenuEntry> Push,
    IReadOnlyList<MenuEntry> Merge,
    IReadOnlyList<MenuEntry> Rename,
    IReadOnlyList<MenuEntry> Delete,
    IReadOnlyList<MenuEntry> DeleteTags,
    IReadOnlyList<MenuEntry> SelectInLeftPanel);

// The grid menu's items (upstream's RevisionGridControl context menu, MVVM.md): the ref submenus and the copy submenu
// listed for the selected commit, and the commands of its stash, advanced, compare and ref items.
public sealed partial class BrowseToolbarViewModel
{
    /// <summary>
    ///  The ref submenus' entries for the selected commit; an entry runs its submenu's command on its ref.
    /// </summary>
    public GridRefMenuEntries GridRefEntries(GridRefMenus menus) => new(
        RefEntries(menus.Checkout, CheckoutRefCommand),
        RefEntries(menus.Push, PushRefCommand),
        RefEntries(menus.Merge, MergeRefCommand),
        RefEntries(menus.Rename, RenameRefCommand),
        RefEntries(menus.Delete, DeleteRefCommand),
        RefEntries(menus.DeleteTags, DeleteTagRefCommand),
        menus.SelectInLeftPanel.Count == 1 ? [] : RefEntries(menus.SelectInLeftPanel, SelectInLeftPanelCommand));

    /// <summary>
    ///  Upstream's <c>CopyContextMenuItem</c> for <paramref name="rows"/>: an entry copies its value; a caption is bold and
    ///  copies nothing.
    /// </summary>
    /// <param name="keep">Which labels to list (the focused label menu lists fewer); null for all.</param>
    public static IReadOnlyList<MenuEntry> CopyEntries(IReadOnlyList<CommitRow> rows, string branchesText,
        string tagsText,
        Func<RefLabel, bool>? keep, ICommand copy)
        =>
        [
            .. GridMenus.CopyItems(rows, branchesText, tagsText, keep).Select(item => item is null
                ? MenuEntry.Separator()
                : new MenuEntry(item.Text, copy, item.Value)
                {
                    HasAccessKey = true,
                    Image = item.Image,

                    // Upstream's CopyContextMenuItem adapts its branch and author pictures.
                    AdaptsImage = item.Image is "Branch" or "Author",
                    IsBold = item.Value is null,
                    IsEnabled = item.Value is not null,
                })
        ];

    [RelayCommand]
    private void CopyText(string? text)
    {
        if (text is not null)
        {
            Run(() => BrowseWindow.CopyTextAsync(text));
        }
    }

    [RelayCommand]
    private void CheckoutRef(GridMenuRef gitRef) => OnRepository(path => gitRef.IsRemote
        ? BrowseWindow.CheckoutRemoteBranchAsync(path, gitRef.Name)
        : BrowseWindow.CheckoutNamedBranchAsync(path, gitRef.Name));

    [RelayCommand]
    private void PushRef(GridMenuRef gitRef) => Run(() => BrowseWindow.PushBranchAsync(gitRef.Name));

    [RelayCommand]
    private void MergeRef(GridMenuRef gitRef) =>
        OnRepository(path => BrowseWindow.MergeBranchAsync(path, gitRef.Target));

    [RelayCommand]
    private void RenameRef(GridMenuRef gitRef) => Run(() => BrowseWindow.RenameBranchAsync(gitRef.Name));

    [RelayCommand]
    private void DeleteRef(GridMenuRef gitRef) => OnRepository(path => gitRef.IsRemote
        ? BrowseWindow.DeleteRemoteBranchAsync(path, gitRef.Name)
        : BrowseWindow.DeleteBranchesAsync(path, [gitRef.Name]));

    [RelayCommand]
    private void DeleteTagRef(GridMenuRef gitRef) =>
        OnRepository(path => BrowseWindow.DeleteNamedTagAsync(path, gitRef.Name));

    // Upstream's tsmiSelectInLeftPanel: the item selects its single ref (null when it has a submenu), a sub-item its own.
    [RelayCommand]
    private void SelectInLeftPanel(GridMenuRef? gitRef)
    {
        if (gitRef is not null)
        {
            BrowseWindow.SelectInLeftPanel(gitRef.Name, gitRef.IsRemote);
        }
    }

    [RelayCommand]
    private void ResetAnotherBranch() => OnSelectedCommit(BrowseWindow.ResetAnotherBranchAsync);

    // Upstream's ApplyStashToolStripMenuItemClick (by the stash's commit), PopStashToolStripMenuItemClick and
    // DropStashToolStripMenuItemClick (by its selector, the drop after upstream's suppressible question).
    [RelayCommand]
    private void ApplySelectedStash() => OnSelectedCommit(_actions.ApplyStashAsync);

    [RelayCommand]
    private void PopSelectedStash() => OnSelectedStash(_actions.PopStashAsync);

    [RelayCommand]
    private void DropSelectedStash() => OnSelectedStash(async (path, stash)
        => await BrowseWindow.ConfirmDropStashAsync() && await _actions.DropStashAsync(path, stash));

    // Upstream's editCommitToolStripMenuItem_Click and rewordCommitToolStripMenuItem_Click (LaunchRebase): an interactive
    // rebase onto the commit's first parent that stops at the commit to edit or reword it.
    [RelayCommand]
    private void RewriteSelectedCommit(string command)
    {
        if (OpenPath is not { } path || _commits.Selected is not { } row)
        {
            return;
        }

        ObjectId? parent = row.ParentHashes is [{ } first, ..] && ObjectId.TryParse(first, out ObjectId id) ? id : null;
        Run(() => _actions.RewriteCommitAsync(path,
            RewriteCommit.Arguments(parent, GitVersion.Current.SupportRebaseMerges), command));
    }

    [RelayCommand]
    private void RewriteCommitHelp() =>
        BrowseWindow.ShowUserManual(RewriteCommit.HelpSubfolder, RewriteCommit.HelpAnchor);

    // Upstream's OutputHistoryControl menu texts.
    public const string CopyOutputText = "Copy";
    public const string ClearOutputText = "Clear";

    /// <summary>
    ///  Upstream's output history menu: copy <paramref name="text"/> (the selection, else the whole history), clear it.
    /// </summary>
    public IReadOnlyList<MenuEntry> OutputHistoryMenu(string? text)
        =>
        [
            new(CopyOutputText, CopyTextCommand, text) { HasAccessKey = true },
            new(ClearOutputText, ClearOutputHistoryCommand) { HasAccessKey = true },
        ];

    [RelayCommand]
    private void ClearOutputHistory() => _actions.History?.Clear();

    [RelayCommand]
    private void CloneFromHost(string hostName) => BrowseWindow.CloneFromHost(hostName);

    [RelayCommand]
    private void ExecutePlugin(IGitPlugin plugin) => BrowseWindow.ExecutePlugin(plugin);

    [RelayCommand]
    private void RunUserScript(ScriptDefinition script) => OnRepository(path => BrowseWindow.RunUserScriptAsync(path, script));

    // Upstream's AddUserScripts in the grid's and the left panel's menus: the script on the grid's selection, while a commit
    // is selected.
    [RelayCommand]
    private void RunGridScript(ScriptDefinition script)
    {
        if (_commits.Selected is not null)
        {
            OnRepository(path => BrowseWindow.RunUserScriptAsync(path, script));
        }
    }

    // The grid's hotkey commands that are menu items too: the compare items, the special commits, the Navigate menu.
    [RelayCommand]
    private void RunGrid(GridCommand command) => BrowseWindow.RunGridCommand(command);

    private void OnSelectedStash(Func<string, string, Task<bool>> action)
    {
        if (OpenPath is { } path && _commits.Selected?.StashLabel is { } stash)
        {
            Run(() => action(path, stash));
        }
    }

    /// <summary>
    ///  Upstream's <c>DeleteRef</c> and <c>RenameRef</c> hotkeys (<c>InitiateRefAction</c>) on the selected commit: with one
    ///  branch or tag to act on, the action runs at once and nothing is returned; with several, the entries to choose
    ///  from, as upstream's <c>FormQuickGitRefSelector</c> lists them.
    /// </summary>
    public IReadOnlyList<MenuEntry> QuickRefAction(GridRefMenus menus, bool delete)
    {
        IReadOnlyList<MenuEntry> entries = delete
            ? [.. RefEntries(menus.Delete.OfType<GridMenuRef>(), DeleteRefCommand),
                .. RefEntries(menus.DeleteTags, DeleteTagRefCommand)]
            : RefEntries(menus.Rename, RenameRefCommand);
        if (entries is [{ Command: { } command, CommandParameter: var parameter }])
        {
            command.Execute(parameter);
            return [];
        }

        return entries;
    }

    private static IReadOnlyList<MenuEntry> RefEntries(IEnumerable<GridMenuRef?> refs, ICommand command)
        =>
        [
            .. refs.Select(gitRef =>
                gitRef is null
                    ? MenuEntry.Separator()
                    : new MenuEntry(gitRef.Name, command, gitRef) { Image = gitRef.Image })
        ];
}
