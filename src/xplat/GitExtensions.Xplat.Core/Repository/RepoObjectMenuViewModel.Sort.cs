using CommunityToolkit.Mvvm.Input;
using GitCommands.Utils;
using GitExtensions.Xplat.Core.Presentation;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Repository;

// The left panel's sort menus (upstream's RepoObjectsTree sort items, MVVM.md): every choice by its description, the
// current one checked; a choice applies at once.
public sealed partial class RepoObjectMenuViewModel
{
    /// <summary>
    ///  Upstream's "Sort by" choices, <paramref name="current"/> checked.
    /// </summary>
    public IReadOnlyList<MenuEntry> SortByMenu(GitRefsSortBy current)
        =>
        [
            .. Enum.GetValues<GitRefsSortBy>().Select(sortBy =>
                new MenuEntry(sortBy.GetDescription(), RunSortByCommand, sortBy)
                {
                    HasAccessKey = true, IsChecked = sortBy == current, IsRadio = true,
                })
        ];

    /// <summary>
    ///  Upstream's "Sort order" choices, <paramref name="current"/> checked.
    /// </summary>
    public IReadOnlyList<MenuEntry> SortOrderMenu(GitRefsSortOrder current)
        =>
        [
            .. Enum.GetValues<GitRefsSortOrder>().Select(order =>
                new MenuEntry(order.GetDescription(), RunSortOrderCommand, order)
                {
                    HasAccessKey = true, IsChecked = order == current, IsRadio = true,
                })
        ];

    [RelayCommand]
    private void RunSortBy(GitRefsSortBy sortBy) => Window.SetRefsSortBy(sortBy);

    [RelayCommand]
    private void RunSortOrder(GitRefsSortOrder order) => Window.SetRefsSortOrder(order);
}
