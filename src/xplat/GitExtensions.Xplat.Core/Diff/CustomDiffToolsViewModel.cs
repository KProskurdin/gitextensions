using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using ResourceManager;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's <c>CustomDiffMergeToolProvider.LoadCustomDiffMergeTools</c> for a window's menu items: under each item, the
///  diff (or merge) tools git can start here when there are several, the default (first) bold; for diff tools only while
///  "Show all available difftools" is on, with upstream's "Disable this dropdown", which turns the setting off. The item
///  itself opens the default tool.
/// </summary>
public sealed class CustomDiffToolsViewModel
{
    private readonly List<CustomDiffToolMenu> _menus = [];
    private readonly IAppPreferences _preferences;
    private readonly IDiffMergeToolCatalog _catalog;
    private readonly bool _diff;
    private readonly Func<string> _disableText;
    private bool _loaded;

    /// <param name="diff">Diff tools, else merge tools.</param>
    /// <param name="disableText">Upstream's "Disable this dropdown", translated.</param>
    public CustomDiffToolsViewModel(IAppPreferences preferences, IDiffMergeToolCatalog catalog, bool diff = true,
        Func<string>? disableText = null)
    {
        _preferences = preferences;
        _catalog = catalog;
        _diff = diff;
        _disableText = disableText ?? (() => TranslatedStrings.DisableMenuItem);
        DisableCommand = new RelayCommand(Disable);
    }

    /// <summary>
    ///  Upstream's "Disable this dropdown": turns "Show all available difftools" off and empties the items.
    /// </summary>
    public IRelayCommand DisableCommand { get; }

    /// <summary>
    ///  Adds a menu item whose tools <paramref name="open"/> opens; it gets the tools when they are read.
    /// </summary>
    /// <param name="open">Opens the selection in a tool, null for the default one. It shows its own errors.</param>
    public CustomDiffToolMenu Add(Func<string?, Task> open)
    {
        CustomDiffToolMenu menu = new(open);
        _menus.Add(menu);
        return menu;
    }

    /// <summary>
    ///  Reads the tools once (upstream loads them when the menu is first used) and lists them under every item; a later
    ///  call does nothing, so a failure is reported once.
    /// </summary>
    public Task LoadAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        _loaded = true;
        return ReadAsync();
    }

    private async Task ReadAsync()
    {
        if (_diff && !_preferences.ShowAvailableDiffTools)
        {
            return;
        }

        IReadOnlyList<string> tools = await _catalog.GetAvailableAsync(_diff);
        if (tools.Count <= 1)
        {
            return;
        }

        foreach (CustomDiffToolMenu menu in _menus)
        {
            List<MenuEntry> entries =
            [
                .. tools.Select((tool, index) =>
                    new MenuEntry(tool, menu.OpenCommand, tool) { IsBold = index == 0, SearchText = tool }),
            ];
            if (_diff)
            {
                entries.Add(MenuEntry.Separator());
                entries.Add(new MenuEntry(_disableText(), DisableCommand) { HasAccessKey = true });
            }

            menu.Entries = entries;
        }
    }

    private void Disable()
    {
        _preferences.ShowAvailableDiffTools = false;
        _preferences.Save();
        foreach (CustomDiffToolMenu menu in _menus)
        {
            menu.Entries = [];
        }
    }
}

/// <summary>
///  One menu item of <see cref="CustomDiffToolsViewModel"/>: its tools as entries, and the command that opens the
///  selection in one (its parameter the tool's name, null for git's default tool, as the item itself runs it).
/// </summary>
public sealed partial class CustomDiffToolMenu : ObservableObject
{
    private readonly Func<string?, Task> _open;

    internal CustomDiffToolMenu(Func<string?, Task> open)
    {
        _open = open;
    }

    /// <summary>
    ///  The item's tools; empty while there is no choice to offer.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<MenuEntry> Entries { get; set; } = [];

    // Concurrent: a tool open does not disable the item, whose submenu could then not open.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task OpenAsync(string? tool) => _open(tool);
}
