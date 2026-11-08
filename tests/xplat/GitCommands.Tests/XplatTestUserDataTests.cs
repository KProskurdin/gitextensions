using System.Windows.Forms;
using CommonTestUtils;
using GitCommands;
using NUnit.Framework;

namespace GitCommandsTests;

/// <summary>
///  Tests must never reach the settings of the user running them: <c>TestAppSettingsAttribute</c> deletes the settings file,
///  which on macOS once was the user's own.
/// </summary>
internal sealed class XplatTestUserDataTests
{
    [Test]
    public void The_settings_file_should_be_in_the_tests_own_folder()
    {
        string settingsFile = AppSettings.SettingsContainer.SettingsCache.SettingsFilePath;

        XplatTestUserData.Contains(settingsFile).Should().BeTrue();
        settingsFile.Should().NotStartWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
    }

    [Test]
    public void The_variable_should_be_the_one_the_shim_reads()
    {
        Environment.GetEnvironmentVariable(Application.UserDataFolderVariable).Should().NotBeNullOrEmpty();
        XplatTestUserData.Contains(Path.Combine(Application.UserAppDataPath, "file")).Should().BeTrue();
    }
}
