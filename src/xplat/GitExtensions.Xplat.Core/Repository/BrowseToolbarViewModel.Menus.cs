using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

// The toolstrip's drop-downs filled as they open (MVVM.md): their entries, from what the window read (the terminals,
// the submodules' structure, the worktrees, the branches), and the commands the entries run.
public sealed partial class BrowseToolbarViewModel
{
    /// <summary>
    ///  Upstream's FillUserShells: a terminal per installed one, opened in the working directory.
    /// </summary>
    public IReadOnlyList<MenuEntry> TerminalMenu(IReadOnlyList<TerminalChoice> terminals)
        =>
        [
            .. terminals.Select(terminal => new MenuEntry(terminal.Name, OpenTerminalChoiceCommand, terminal)
            {
                Image = terminal.Image, Tip = terminal.Name,
            })
        ];

    /// <summary>
    ///  Upstream's submodule drop-down (PopulateToolbar): the projects (the shown one bold), "Update all submodules" and
    ///  the current submodule's update; "Loading..." until the structure is read.
    /// </summary>
    public IReadOnlyList<MenuEntry> SubmodulesMenu(SubmoduleStructure? structure)
    {
        if (_commits.RepositoryPath is null)
        {
            return [];
        }

        if (structure is null)
        {
            return [new MenuEntry(_translate(SubmoduleStructure.LoadingText))];
        }

        return [.. structure.MenuItems(_translate).Select(entry => entry switch
        {
            { Kind: SubmoduleMenuItemKind.Separator } => MenuEntry.Separator(),
            { Kind: SubmoduleMenuItemKind.Project, Project: { } project } =>
                new MenuEntry(entry.Text, OpenSubmoduleProjectCommand, project)
                {
                    IsBold = project.IsCurrent, Image = project.Image,
                },
            { Kind: SubmoduleMenuItemKind.UpdateAll } =>
                new MenuEntry(AccessKeyText(entry.Text), UpdateAllSubmodulesCommand)
                {
                    HasAccessKey = true, Image = "SubmodulesUpdate",
                },
            { Kind: SubmoduleMenuItemKind.UpdateCurrent } =>
                new MenuEntry(entry.Text, UpdateCurrentSubmoduleCommand, structure) { Image = "FolderSubmodule" },
            _ => new MenuEntry(entry.Text),
        })];
    }

    /// <summary>
    ///  Upstream's toolStripWorktrees_DropDownOpening: every worktree by its folder's name (the open one checked, those
    ///  whose folders are gone disabled), then Create worktree..., Prune worktrees and Manage worktrees....
    /// </summary>
    public IReadOnlyList<MenuEntry> WorktreesMenu(IReadOnlyList<WorktreeInfo> worktrees, string repositoryPath)
    {
        List<MenuEntry> entries = [];
        foreach (WorktreeInfo worktree in worktrees)
        {
            bool current = ManageWorktreeDialog.IsCurrent(worktree, repositoryPath);
            entries.Add(new MenuEntry(ManageWorktreeDialog.MenuText(worktree), OpenWorktreeFolderCommand, worktree.Path)
            {
                Image = "WorkTree", IsChecked = current ? true : null, IsEnabled = !current && !worktree.IsDeleted,
            });
        }

        entries.Add(MenuEntry.Separator());
        entries.Add(new MenuEntry(_translate(ManageWorktreeDialog.CreateWorktree), CreateWorktreeCommand)
        {
            HasAccessKey = true, Image = "WorkTree",
        });
        entries.Add(new MenuEntry(_translate(ManageWorktreeDialog.PruneWorktrees), PruneWorktreesCommand)
        {
            HasAccessKey = true,
        });
        entries.Add(new MenuEntry(_translate(ManageWorktreeDialog.ManageWorktrees), ManageWorktreesCommand)
        {
            HasAccessKey = true,
        });
        return entries;
    }

    /// <summary>
    ///  Upstream's CurrentBranchDropDownOpening: "Checkout branch..." with its hotkey, then the local branches; a branch
    ///  whose commit the grid does not show has the closed eye and gray text. A click checks the branch out.
    /// </summary>
    /// <param name="checkoutText">The menu's "Checkout branch..." text, with its access key.</param>
    /// <param name="checkoutGesture">Its hotkey, as the view's key gesture.</param>
    public IReadOnlyList<MenuEntry> BranchesMenu(IReadOnlyList<(string Name, string Hash)> branches, string checkoutText,
        object? checkoutGesture)
    {
        List<MenuEntry> entries =
        [
            new(checkoutText, CheckoutBranchCommand) { HasAccessKey = true, Image = "BranchCheckout", Gesture = checkoutGesture },
            MenuEntry.Separator(),
        ];
        foreach ((string name, string hash) in branches)
        {
            bool shown = _commits.IndexOfHash(hash) is not null;
            entries.Add(new MenuEntry(name, CheckoutNamedBranchCommand, name)
            {
                Image = shown ? "Branch" : "EyeClosed", AdaptsImage = true, IsDimmed = !shown,
            });
        }

        return entries;
    }

    /// <summary>
    ///  Upstream's menu text with its access key as the view shows it (upstream marks it with an ampersand).
    /// </summary>
    public Func<string, string> AccessKeyText { get; init; } = text => text;

    [RelayCommand]
    private void OpenTerminalChoice(TerminalChoice terminal) => OnRepositoryFolder(path => BrowseWindow.OpenTerminal(terminal, path));

    [RelayCommand]
    private void OpenSubmoduleProject(SubmoduleProject project) => Run(() => BrowseWindow.OpenSubmoduleProjectAsync(project));

    [RelayCommand]
    private void UpdateCurrentSubmodule(SubmoduleStructure structure)
        => Run(() => BrowseWindow.UpdateCurrentSubmoduleAsync(structure));

    [RelayCommand]
    private void OpenWorktreeFolder(string worktreePath) => Run(() => BrowseWindow.OpenWorktreeFolderAsync(worktreePath));

    [RelayCommand]
    private void CreateWorktree() => Run(BrowseWindow.CreateWorktreeAsync);

    [RelayCommand]
    private void PruneWorktrees() => OnRepository(BrowseWindow.PruneWorktreesAsync);

    [RelayCommand]
    private void CheckoutNamedBranch(string branch) => OnRepository(path => BrowseWindow.CheckoutNamedBranchAsync(path, branch));
}
