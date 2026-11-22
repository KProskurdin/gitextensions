using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Platform;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  An item of the shell extension's context menu: at the top level (checked), in the cascaded "Git Extensions" menu
///  (neither) or not in the menu (unchecked).
/// </summary>
public sealed partial class ShellMenuEntry(string text, bool? state) : ObservableObject
{
    public string Text { get; } = text;

    /// <summary>
    ///  True at the top level, null in the cascaded menu, false not in the menu.
    /// </summary>
    [ObservableProperty]
    public partial bool? State { get; set; } = state;

    /// <summary>
    ///  Upstream's <c>chlMenuEntries_ItemCheck</c>: a click takes an item at the top level out of the menu, an item out of
    ///  the menu into the cascaded one, and a cascaded item to the top level.
    /// </summary>
    public static bool? Next(bool? state)
        => state switch
        {
            true => false,
            false => null,
            null => true,
        };
}

/// <summary>
///  The view model of upstream's <c>ShellExtensionSettingsPage</c> (Windows only): registering the Explorer shell
///  extension, and which of its context menu items show at the top level, in the cascaded menu or not at all.
/// </summary>
public sealed partial class ShellExtensionPageViewModel : ObservableObject
{
    /// <summary>
    ///  Upstream's menu items (<c>_NO_TRANSLATE_chlMenuEntries</c>), in the order of the stored setting's characters.
    /// </summary>
    public static readonly IReadOnlyList<string> MenuItems =
    [
        "Add files...",
        "Apply patch...",
        "Open repository",
        "Create branch...",
        "Checkout branch...",
        "Checkout revision...",
        "Clone...",
        "Commit...",
        "Create new repository...",
        "Open with difftool",
        "File history",
        "Pull/Fetch...",
        "Push...",
        "Reset file changes..",
        "Revert",
        "Settings",
        "View stash",
        "View changes",
    ];

    // Upstream's texts.
    public const string NoItems = "no items";

    public const string MenuHelp = "* Checked: at top level for direct access\n" +
                                   "* Intermediate: in a cascaded context menu\n" +
                                   "* Unchecked: not added to the menu";

    /// <summary>
    ///  Upstream's <c>menuHelp_Click</c> manual section (subfolder, anchor).
    /// </summary>
    public const string HelpSubfolder = "settings";

    public const string HelpAnchor = "shell-extension";

    // Upstream's characters of CascadeShellMenuItems.
    private const char InMenu = '0';
    private const char InSubMenu = '1';
    private const char NotInMenu = '2';

    // Upstream's preview: "GitExt " before a top level item, the cascaded ones indented under "Git Extensions > ".
    private const string TopLevelPrefix = "GitExt ";
    private const string CascadedHeader = "Git Extensions > ";
    private const string CascadedIndent = "       ";

    private readonly IWindowsInstallation _installation;
    private readonly Action<Exception> _showError;
    private readonly Action _showHelp;
    private readonly Func<string, string> _translate;

    /// <param name="showHelp">Opens upstream's manual on the page's section.</param>
    /// <param name="translate">Upstream's translation of an English text.</param>
    public ShellExtensionPageViewModel(IWindowsInstallation installation, Action<Exception> showError, Action showHelp,
        Func<string, string>? translate = null)
    {
        _installation = installation;
        _showError = showError;
        _showHelp = showHelp;
        _translate = translate ?? (text => text);
        Entries = [.. MenuItems.Select(item => new ShellMenuEntry(item, state: false))];
        foreach (ShellMenuEntry entry in Entries)
        {
            entry.PropertyChanged += OnEntryChanged;
        }

        UpdatePreview();
    }

    public IReadOnlyList<ShellMenuEntry> Entries { get; }

    /// <summary>
    ///  Upstream's "Always show all commands".
    /// </summary>
    [ObservableProperty]
    public partial bool AlwaysShowAllCommands { get; set; }

    /// <summary>
    ///  Upstream's "Context menu preview:" text.
    /// </summary>
    [ObservableProperty]
    public partial string Preview { get; private set; } = "";

    /// <summary>
    ///  Upstream's "Windows Explorer integration" group is enabled when the extension's files are found.
    /// </summary>
    [ObservableProperty]
    public partial bool FilesExist { get; private set; }

    /// <summary>
    ///  Upstream's "Enable shell extension" button is enabled while the extension is not registered.
    /// </summary>
    [ObservableProperty]
    public partial bool CanRegister { get; private set; }

    /// <summary>
    ///  Upstream's <c>SettingsToPage</c>.
    /// </summary>
    public void Load(IAppPreferences preferences)
    {
        string stored = preferences.CascadeShellMenuItems;
        for (int i = 0; i < Entries.Count && i < stored.Length; i++)
        {
            switch (stored[i])
            {
                case InMenu:
                    Entries[i].State = true;
                    break;
                case InSubMenu:
                    Entries[i].State = null;
                    break;
                case NotInMenu:
                    Entries[i].State = false;
                    break;
            }
        }

        AlwaysShowAllCommands = preferences.AlwaysShowAllCommands;
    }

    /// <summary>
    ///  Upstream's <c>PageToSettings</c>.
    /// </summary>
    public void Store(IAppPreferences preferences)
    {
        preferences.CascadeShellMenuItems = string.Concat(Entries.Select(entry => entry.State switch
        {
            true => InMenu,
            null => InSubMenu,
            false => NotInMenu,
        }));
        preferences.AlwaysShowAllCommands = AlwaysShowAllCommands;
    }

    /// <summary>
    ///  Upstream's <c>UpdateRegistrationStatus</c>.
    /// </summary>
    public void UpdateRegistrationStatus()
    {
        FilesExist = _installation.ShellExtensionFilesExist();
        CanRegister = !_installation.IsShellExtensionRegistered();
    }

    [RelayCommand]
    private Task RegisterAsync() => RunAsync(_installation.RegisterShellExtension);

    [RelayCommand]
    private Task UnregisterAsync() => RunAsync(_installation.UnregisterShellExtension);

    [RelayCommand]
    private void ShowHelp() => _showHelp();

    // regsvr32 waits for the elevation prompt, so it runs off the UI thread; upstream shows the status after it either way.
    private async Task RunAsync(Action action)
    {
        try
        {
            await Task.Run(action);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _showError(ex);
        }

        UpdateRegistrationStatus();
    }

    private void OnEntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellMenuEntry.State))
        {
            UpdatePreview();
        }
    }

    // Upstream's UpdatePreview.
    private void UpdatePreview()
    {
        StringBuilder topLevel = new();
        StringBuilder cascaded = new();
        foreach (ShellMenuEntry entry in Entries)
        {
            switch (entry.State)
            {
                case true:
                    topLevel.Append(TopLevelPrefix).Append(entry.Text).Append(Environment.NewLine);
                    break;
                case null:
                    cascaded.Append(CascadedIndent).Append(entry.Text).Append(Environment.NewLine);
                    break;
            }
        }

        string preview = topLevel.ToString();
        if (cascaded.Length > 0)
        {
            preview += CascadedHeader + Environment.NewLine + cascaded;
        }
        else if (topLevel.Length == 0)
        {
            preview += $"({_translate(NoItems)})";
        }

        Preview = preview;
    }
}
