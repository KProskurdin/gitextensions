using System.Globalization;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.App;

// Upstream's DetailedSettingsPage, a DistributedSettingsPage: the push and merge window settings (DetailedSettings) at the
// level the header shows. The revision graph settings are the user's and live in the preferences (see SaveAsync).
public partial class SettingsWindow
{
    private SettingLevel _detailedLevel = SettingLevel.Effective;

    private SettingsSource DetailedSettingsSource => DistributedSettingsAt(_detailedLevel).Settings;

    // Upstream's SettingsToPage for the push and merge window groups.
    private void ShowDetailedValues()
    {
        SettingsSource settings = DetailedSettingsSource;
        chkRemotesFromServer.IsChecked = DetailedSettings.GetRemoteBranchesDirectlyFromRemote[settings];
        addLogMessages.IsChecked = DetailedSettings.AddMergeLogMessages[settings];
        nbMessages.Text = DetailedSettings.MergeLogMessagesCount[settings] is int count
            ? count.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    // Upstream's PageToSettings: an undecided box or an empty count stores nothing, so a lower level decides; the
    // effective settings are shown read-only, so nothing is stored from them.
    private void SaveDetailedPage()
    {
        if (_detailedLevel == SettingLevel.Effective)
        {
            return;
        }

        SettingsSource settings = DetailedSettingsSource;
        DetailedSettings.GetRemoteBranchesDirectlyFromRemote[settings] = chkRemotesFromServer.IsChecked;
        DetailedSettings.AddMergeLogMessages[settings] = addLogMessages.IsChecked;
        DetailedSettings.MergeLogMessagesCount[settings] =
            int.TryParse(nbMessages.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                ? count
                : null;
    }

    // As upstream's DistributedSettingsPage.SetCurrentSettings: the values go to the level they were edited at.
    private void ChangeDetailedLevel(SettingLevel level)
    {
        SaveDetailedPage();
        _detailedLevel = level;
        ShowDetailedValues();
    }
}
