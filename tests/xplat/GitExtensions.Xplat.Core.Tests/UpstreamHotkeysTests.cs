using AwesomeAssertions;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;
using Keys = System.Windows.Forms.Keys;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class UpstreamHotkeysTests
{
    // Written by upstream's XmlSerializer for HotkeySettings[] (GitUI/Hotkey/HotkeySettingsManager.cs).
    private const string UpstreamSetting = """
                                           <?xml version="1.0" encoding="utf-16"?>
                                           <ArrayOfHotkeySettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                                             <HotkeySettings Name="Browse">
                                               <Commands>
                                                 <HotkeyCommand CommandCode="7" Name="Commit" KeyData="Space Control" />
                                                 <HotkeyCommand CommandCode="11" Name="QuickFetch" KeyData="Back Space Down Shift Control" />
                                                 <HotkeyCommand CommandCode="20" Name="OpenSettings" KeyData="MButton Back Clear ShiftKey Capital CapsLock FinalMode IMEConvert Space Home Down Snapshot PrintScreen D0 D4 D8 F17 F21 NumLock LShiftKey LMenu BrowserRefresh BrowserHome MediaNextTrack LaunchMail Oemcomma Control" />
                                                 <HotkeyCommand CommandCode="0" Name="GitBash" KeyData="None" />
                                               </Commands>
                                             </HotkeySettings>
                                             <HotkeySettings Name="Commit">
                                               <Commands>
                                                 <HotkeyCommand CommandCode="11" Name="StageAll" KeyData="LButton RButton Cancel ShiftKey ControlKey Menu Pause A B C P Q R S Control" />
                                               </Commands>
                                             </HotkeySettings>
                                           </ArrayOfHotkeySettings>
                                           """;

    // The section names must be upstream's, or the two apps silently keep separate hotkeys (they once differed here).
    [TestCase(UpstreamHotkeys.BrowseFormName, "CommandsDialogs/FormBrowse.cs")]
    [TestCase(UpstreamHotkeys.CommitFormName, "CommandsDialogs/FormCommit.cs")]
    [TestCase(UpstreamHotkeys.RevisionGridName, "UserControls/RevisionGrid/RevisionGridControl.cs")]
    [TestCase(UpstreamHotkeys.FileViewerName, "Editor/FileViewer.cs")]
    [TestCase(UpstreamHotkeys.LeftPanelName, "LeftPanel/RepoObjectsTree.cs")]
    [TestCase(UpstreamHotkeys.ResolveConflictsName, "CommandsDialogs/FormResolveConflicts.cs")]
    [TestCase(UpstreamHotkeys.ScriptsName, "CommandsDialogs/FormSettings.cs")]
    public void Section_names_should_match_upstreams_HotkeySettingsName(string name, string upstreamFile)
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "app", "GitUI", upstreamFile));

        source.Should().Contain($"HotkeySettingsName = \"{name}\";");
    }

    [Test]
    public void Read_should_turn_the_serializer_flag_lists_back_into_the_keys()
    {
        IReadOnlyDictionary<string, UpstreamHotkey> hotkeys =
            UpstreamHotkeys.Read(UpstreamSetting, UpstreamHotkeys.BrowseFormName);

        hotkeys["Commit"].Should().Be(new UpstreamHotkey(7, "Commit", Keys.Control | Keys.Space));
        hotkeys["QuickFetch"].KeyData.Should().Be(Keys.Control | Keys.Shift | Keys.Down);
        hotkeys["OpenSettings"].KeyData.Should().Be(Keys.Control | Keys.Oemcomma);
        hotkeys["GitBash"].KeyData.Should().Be(Keys.None);
        UpstreamHotkeys.Read(UpstreamSetting, UpstreamHotkeys.CommitFormName)["StageAll"].KeyData.Should()
            .Be(Keys.Control | Keys.S);
    }

    [Test]
    public void Write_should_change_only_the_given_commands_and_keep_other_forms()
    {
        string written = UpstreamHotkeys.Write(UpstreamSetting, UpstreamHotkeys.BrowseFormName,
            [new UpstreamHotkey(7, "Commit", Keys.Control | Keys.Shift | Keys.K)]);

        UpstreamHotkeys.Read(written, UpstreamHotkeys.BrowseFormName)["Commit"].KeyData.Should()
            .Be(Keys.Control | Keys.Shift | Keys.K);
        UpstreamHotkeys.Read(written, UpstreamHotkeys.BrowseFormName)["OpenSettings"].KeyData.Should()
            .Be(Keys.Control | Keys.Oemcomma);
        UpstreamHotkeys.Read(written, UpstreamHotkeys.CommitFormName)["StageAll"].KeyData.Should()
            .Be(Keys.Control | Keys.S);
        written.Should().Contain("KeyData=\"K Shift Control\"");
    }

    [Test]
    public void Write_should_create_the_setting_when_there_is_none()
    {
        string written = UpstreamHotkeys.Write(null, UpstreamHotkeys.BrowseFormName,
            [new UpstreamHotkey(40, "Push", Keys.Control | Keys.Up)]);

        written.Should().StartWith("<?xml");
        UpstreamHotkeys.Read(written, UpstreamHotkeys.BrowseFormName)["Push"].KeyData.Should()
            .Be(Keys.Control | Keys.Up);
    }

    [Test]
    public void ReadByCode_should_key_the_hotkeys_by_command_code_as_upstream_matches_them()
    {
        IReadOnlyDictionary<int, UpstreamHotkey> hotkeys =
            UpstreamHotkeys.ReadByCode(UpstreamSetting, UpstreamHotkeys.BrowseFormName);

        hotkeys[7].Should().Be(new UpstreamHotkey(7, "Commit", Keys.Control | Keys.Space));
    }

    [Test]
    public void Write_should_replace_a_command_by_its_code_so_a_renamed_script_keeps_one_entry()
    {
        string first = UpstreamHotkeys.Write(null, UpstreamHotkeys.ScriptsName,
            [new UpstreamHotkey(9001, "Old name", Keys.F7)]);

        string renamed = UpstreamHotkeys.Write(first, UpstreamHotkeys.ScriptsName,
            [new UpstreamHotkey(9001, "New name", Keys.F8)]);

        UpstreamHotkeys.ReadByCode(renamed, UpstreamHotkeys.ScriptsName).Should().ContainSingle()
            .Which.Value.Should().Be(new UpstreamHotkey(9001, "New name", Keys.F8));
    }

    [Test]
    public void A_damaged_setting_reads_as_no_hotkeys()
    {
        UpstreamHotkeys.Read("<ArrayOfHotkeySettings><HotkeySettings", UpstreamHotkeys.BrowseFormName).Should()
            .BeEmpty();
    }

    // The test's own source file is in the repository, also when the build output is not (Linux builds).
    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", ".."));
}
