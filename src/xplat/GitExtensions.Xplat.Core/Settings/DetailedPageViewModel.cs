using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The view model of upstream's <c>DetailedSettingsPage</c>, a <c>DistributedSettingsPage</c>: the push and merge window
///  settings (<see cref="DetailedSettings"/>) at the level the page's header shows. An undecided box or an empty count stores
///  nothing, so a lower level decides; the effective settings are shown read-only, so nothing is stored from them.
/// </summary>
/// <param name="settingsAt">The repository's settings at a level, which the window keeps for OK.</param>
public sealed partial class DetailedPageViewModel(Func<SettingLevel, SettingsSource> settingsAt) : ObservableObject
{
    private SettingLevel _level = SettingLevel.Effective;

    [ObservableProperty]
    public partial bool? RemotesFromServer { get; set; }

    [ObservableProperty]
    public partial bool? AddMergeLogMessages { get; set; }

    [ObservableProperty]
    public partial string? MergeLogMessagesCount { get; set; }

    /// <summary>
    ///  Upstream's SettingsToPage for the push and merge window groups, at <paramref name="level"/>.
    /// </summary>
    public void Show(SettingLevel level)
    {
        _level = level;
        SettingsSource settings = settingsAt(level);
        RemotesFromServer = DetailedSettings.GetRemoteBranchesDirectlyFromRemote[settings];
        AddMergeLogMessages = DetailedSettings.AddMergeLogMessages[settings];
        MergeLogMessagesCount = DetailedSettings.MergeLogMessagesCount[settings] is int count
            ? count.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    /// <summary>
    ///  As upstream's DistributedSettingsPage.SetCurrentSettings: the values go to the level they were edited at.
    /// </summary>
    public void ChangeLevel(SettingLevel level)
    {
        Save();
        Show(level);
    }

    /// <summary>
    ///  Upstream's PageToSettings, at the level shown.
    /// </summary>
    public void Save()
    {
        if (_level == SettingLevel.Effective)
        {
            return;
        }

        SettingsSource settings = settingsAt(_level);
        DetailedSettings.GetRemoteBranchesDirectlyFromRemote[settings] = RemotesFromServer;
        DetailedSettings.AddMergeLogMessages[settings] = AddMergeLogMessages;
        DetailedSettings.MergeLogMessagesCount[settings] =
            int.TryParse(MergeLogMessagesCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                ? count
                : null;
    }
}
