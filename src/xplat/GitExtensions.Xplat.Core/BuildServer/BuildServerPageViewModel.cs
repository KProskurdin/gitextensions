using CommunityToolkit.Mvvm.ComponentModel;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.BuildServer;

/// <summary>
///  A choice of the build server type list: a plugin's type, or upstream's "None".
/// </summary>
public sealed record BuildServerTypeOption(string? Name, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
///  The view model of upstream's <c>BuildServerIntegrationSettingsPage</c>, a <c>DistributedSettingsPage</c>: the build
///  server type, whether the integration is on and whether the build result page shows, at the level the page's header
///  shows. The type's own settings are the plugin's control, which the window shows for <see cref="SelectedType"/>.
/// </summary>
public sealed partial class BuildServerPageViewModel : ObservableObject
{
    // Upstream's _noneItem.
    public const string NoBuildServer = "None";

    private readonly Func<SettingLevel, SettingsSource> _settingsAt;
    private readonly Func<Task<IReadOnlyList<BuildServerType>>> _readTypes;
    private bool _showingValues;

    /// <param name="settingsAt">The repository's settings at a level, which the window keeps for OK.</param>
    /// <param name="readTypes">The plugins' build server types; read off the UI thread, as the plugins may still load.</param>
    /// <param name="hasRepository">A repository is open; without one there is nothing to set up.</param>
    public BuildServerPageViewModel(Func<SettingLevel, SettingsSource> settingsAt,
        Func<Task<IReadOnlyList<BuildServerType>>> readTypes, bool hasRepository)
    {
        _settingsAt = settingsAt;
        _readTypes = readTypes;
        HasRepository = hasRepository;
    }

    public bool HasRepository { get; }

    /// <summary>
    ///  The level the page shows and stores at.
    /// </summary>
    public SettingLevel Level { get; private set; } = SettingLevel.Effective;

    /// <summary>
    ///  The settings of the level shown.
    /// </summary>
    public SettingsSource Settings => _settingsAt(Level);

    [ObservableProperty]
    public partial IReadOnlyList<BuildServerTypeOption> Types { get; private set; } = [];

    [ObservableProperty]
    public partial BuildServerTypeOption? SelectedType { get; set; }

    [ObservableProperty]
    public partial bool? IntegrationEnabled { get; set; }

    [ObservableProperty]
    public partial bool? ShowBuildResultPage { get; set; }

    /// <summary>
    ///  True once the types are listed; the page can be edited from then.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; private set; }

    /// <summary>
    ///  Raised when the user chose another type, so the window shows its plugin's settings control.
    /// </summary>
    public event EventHandler? TypeChosen;

    /// <summary>
    ///  Upstream's Init and SettingsToPage: the types, then the values of the level shown.
    /// </summary>
    public async Task LoadAsync()
    {
        IReadOnlyList<BuildServerType> types = await _readTypes();
        Types =
        [
            new BuildServerTypeOption(null, NoBuildServer),
            .. types.Select(type => new BuildServerTypeOption(type.Name, type.DisplayName)),
        ];
        ShowValues();
        IsLoaded = true;
    }

    /// <summary>
    ///  Sets the level before the page is loaded, from the page's header.
    /// </summary>
    public void SetLevel(SettingLevel level) => Level = level;

    /// <summary>
    ///  Shows the values stored at <paramref name="level"/>; the window stores the values shown first.
    /// </summary>
    public void ShowLevel(SettingLevel level)
    {
        Level = level;
        ShowValues();
    }

    /// <summary>
    ///  Upstream's PageToSettings: an undecided check box stores nothing, so a lower level or the detection decides. The
    ///  effective settings are shown read-only, so nothing is stored from them. False when nothing was stored.
    /// </summary>
    public bool Save()
    {
        if (!IsLoaded || Level == SettingLevel.Effective)
        {
            return false;
        }

        SettingsSource settings = Settings;
        BuildServerSettings.ServerName[settings] = SelectedType?.Name;
        BuildServerSettings.IntegrationEnabled[settings] = IntegrationEnabled;
        BuildServerSettings.ShowBuildResultPage[settings] = ShowBuildResultPage;
        return true;
    }

    partial void OnSelectedTypeChanged(BuildServerTypeOption? value)
    {
        if (!_showingValues)
        {
            TypeChosen?.Invoke(this, EventArgs.Empty);
        }
    }

    // Upstream's SettingsToPage, from the settings of the level shown.
    private void ShowValues()
    {
        SettingsSource settings = Settings;
        string? current = BuildServerSettings.ServerName[settings];
        _showingValues = true;
        SelectedType = Types.FirstOrDefault(option => option.Name == current) ?? Types.FirstOrDefault();
        _showingValues = false;
        IntegrationEnabled = BuildServerSettings.IntegrationEnabled[settings];
        ShowBuildResultPage = BuildServerSettings.ShowBuildResultPage[settings];
    }
}
