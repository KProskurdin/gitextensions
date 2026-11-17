using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  What the dashboard's commands (<see cref="DashboardViewModel"/>) ask of the window: opening a repository or a folder,
///  upstream's questions and the category name prompt, and the menus that list the history to read it again.
/// </summary>
public interface IDashboardWindow
{
    void OpenRepository(DashboardRepository repository);

    void OpenFolder(string path);

    /// <summary>
    ///  Upstream's <c>FormDashboardCategoryTitle</c>: a new name, not one of <paramref name="existing"/>; null when none is
    ///  given.
    /// </summary>
    Task<string?> PromptCategoryAsync(IReadOnlyList<string> existing, string? originalName);

    /// <summary>
    ///  A yes or no question, both texts translated.
    /// </summary>
    Task<bool> AskAsync(string question, string caption);

    /// <summary>
    ///  The history changed: the Start menu's recent repositories read it again.
    /// </summary>
    Task RepositoriesChangedAsync();

    void ShowError(string message);
}

/// <summary>
///  A category chosen for a repository in its tile's "Categories" submenu; null for "(none)".
/// </summary>
public sealed record DashboardCategoryChoice(DashboardRepository Repository, string? Category);

// The dashboard's menus (upstream's UserRepositoriesList, MVVM.md): a tile's menu and a group's task link listed as
// entries, and the commands they and the tiles run.
public sealed partial class DashboardViewModel
{
    // Upstream's UserRepositoriesList and FormDashboardCategoryTitle texts.
    public const string RenameCategoryText = "Rename category";
    public const string NoCategoryText = "(none)";
    public const string AddCategoryText = "Add new...";
    public const string DeleteCategoryCaption = "Delete Category";
    public const string DeleteCategoryQuestion =
        "Do you want to delete category \"{0}\" with {1} repositories?\n\nThe action cannot be undone.";
    public const string ClearRecentCaption = "Clear recent repositories";
    public const string ClearRecentQuestion =
        "Do you want to clear the list of recent repositories?\n\nThe action cannot be undone.";
    public const string ShowInFolderText = "Show in folder";
    public const string CategoriesText = "Categories";
    public const string RemoveText = "Remove project from the list";
    public const string RemoveMissingText = "Remove missing projects from the list";
    public const string DeleteCategoryText = "Delete category";
    public const string ClearRecentText = "Clear all recent repositories";

    /// <summary>
    ///  The window the commands ask; set by the window.
    /// </summary>
    public IDashboardWindow? Window { get; set; }

    private IDashboardWindow DashboardWindow => Window ?? throw new InvalidOperationException("The window is not set.");

    /// <summary>
    ///  Upstream's repository menu: show the folder, put it in a category, remove it or the missing ones.
    /// </summary>
    public IReadOnlyList<MenuEntry> RepositoryMenu(DashboardRepository repository)
        =>
        [
            new(_translate(ShowInFolderText), ShowInFolderCommand, repository) { HasAccessKey = true, Image = "BrowseFileExplorer" },
            MenuEntry.Separator(),
            new(_translate(CategoriesText)) { HasAccessKey = true, Children = CategoryMenu(repository) },
            MenuEntry.Separator(),
            new(_translate(RemoveText), RemoveFromListCommand, repository) { HasAccessKey = true },
            new(_translate(RemoveMissingText), RemoveMissingFromListCommand) { HasAccessKey = true, IsVisible = HasInvalid },
        ];

    /// <summary>
    ///  Upstream's group task link: rename or delete a category, or clear the recent repositories.
    /// </summary>
    public IReadOnlyList<MenuEntry> GroupMenu(DashboardGroup group)
        =>
        [
            new(_translate(RenameCategoryText), RenameGroupCommand, group)
            {
                HasAccessKey = true, Image = "FileStatusModified", IsVisible = !group.IsRecent,
            },
            new(_translate(DeleteCategoryText), DeleteGroupCommand, group)
            {
                HasAccessKey = true, Image = "StarRemove", IsVisible = !group.IsRecent,
            },
            new(_translate(ClearRecentText), ClearRecentListCommand) { HasAccessKey = true, Image = "CleanupRepo", IsVisible = group.IsRecent },
        ];

    // Upstream's tsmiCategories_DropDownOpening: "(none)" and the categories when there are any, then "Add new..."; the
    // repository's own category and, without one, "(none)" are disabled.
    private IReadOnlyList<MenuEntry> CategoryMenu(DashboardRepository repository)
    {
        List<MenuEntry> entries = [];
        IReadOnlyList<string> categories = Categories;
        if (categories.Count > 0)
        {
            entries.Add(new MenuEntry(_translate(NoCategoryText), ChooseCategoryCommand,
                new DashboardCategoryChoice(repository, null)) { HasAccessKey = true, IsEnabled = repository.HasCategory });
            entries.AddRange(categories.Select(category => new MenuEntry(category, ChooseCategoryCommand,
                new DashboardCategoryChoice(repository, category)) { HasAccessKey = true, IsEnabled = category != repository.Category }));
            entries.Add(MenuEntry.Separator());
        }

        entries.Add(new MenuEntry(_translate(AddCategoryText), AddCategoryCommand, repository) { HasAccessKey = true, Image = "BulletAdd" });
        return entries;
    }

    [RelayCommand]
    private void OpenTile(DashboardRepository repository) => DashboardWindow.OpenRepository(repository);

    [RelayCommand]
    private void ShowInFolder(DashboardRepository repository) => DashboardWindow.OpenFolder(repository.Path);

    [RelayCommand]
    private void RemoveFromList(DashboardRepository repository) => Change(() => RemoveAsync(repository));

    [RelayCommand]
    private void RemoveMissingFromList() => Change(RemoveMissingAsync);

    [RelayCommand]
    private void ChooseCategory(DashboardCategoryChoice choice)
        => Change(() => AssignCategoryAsync(choice.Repository, choice.Category));

    [RelayCommand]
    private void AddCategory(DashboardRepository repository) => Run(async () =>
    {
        if (await DashboardWindow.PromptCategoryAsync(Categories, originalName: null) is { } name)
        {
            await ChangeAsync(() => AssignCategoryAsync(repository, name));
        }
    });

    [RelayCommand]
    private void RenameGroup(DashboardGroup group) => Run(async () =>
    {
        if (group.Category is { } category
            && await DashboardWindow.PromptCategoryAsync([.. Categories.Where(other => other != category)], category) is { } name)
        {
            await ChangeAsync(() => RenameCategoryAsync(category, name));
        }
    });

    [RelayCommand]
    private void DeleteGroup(DashboardGroup group) => Run(async () =>
    {
        if (group.Category is { } category
            && await DashboardWindow.AskAsync(string.Format(_translate(DeleteCategoryQuestion), category, CountIn(category)),
                _translate(DeleteCategoryCaption)))
        {
            await ChangeAsync(() => RenameCategoryAsync(category, null));
        }
    });

    [RelayCommand]
    private void ClearRecentList() => Run(async () =>
    {
        if (await DashboardWindow.AskAsync(_translate(ClearRecentQuestion), _translate(ClearRecentCaption)))
        {
            await ChangeAsync(ClearRecentAsync);
        }
    });

    private void Change(Func<Task> change) => Run(() => ChangeAsync(change));

    // A change of the history shows in the Start menu's recent repositories too.
    private async Task ChangeAsync(Func<Task> change)
    {
        await change();
        await DashboardWindow.RepositoriesChangedAsync();
    }

    private void Run(Func<Task> action) => _ = RunAsync(action);

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DashboardWindow.ShowError(ex.Message);
        }
    }
}
