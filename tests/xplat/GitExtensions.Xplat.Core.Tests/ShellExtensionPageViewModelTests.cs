using System.Runtime.CompilerServices;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ShellExtensionPageViewModelTests
{
    private readonly List<Exception> _errors = [];
    private InMemoryWindowsInstallation _installation = new();
    private int _helps;

    [SetUp]
    public void SetUp()
    {
        _errors.Clear();
        _installation = new InMemoryWindowsInstallation();
        _helps = 0;
    }

    [Test]
    public void The_texts_and_items_are_upstreams()
    {
        string page = UpstreamSource("ShellExtensionSettingsPage.cs");
        string designer = UpstreamSource("ShellExtensionSettingsPage.Designer.cs");

        page.Should().Contain($"new(\"{ShellExtensionPageViewModel.NoItems}\")")
            .And.Contain($"new(@\"{ShellExtensionPageViewModel.MenuHelp}\")")
            .And.Contain($"UrlFor(\"{ShellExtensionPageViewModel.HelpSubfolder}\", \"{ShellExtensionPageViewModel.HelpAnchor}\")");
        designer.Should().Contain(string.Join(",\n", ShellExtensionPageViewModel.MenuItems.Select(item => $"        \"{item}\"")) + "});");
    }

    [Test]
    public void Load_should_show_each_items_place_from_upstreams_setting_and_Store_should_write_it_back()
    {
        InMemoryAppPreferences preferences = new() { AlwaysShowAllCommands = true };
        ShellExtensionPageViewModel page = New();

        page.Load(preferences);

        page.Entries.Take(4).Select(entry => entry.State).Should().Equal(null, null, true, null);
        page.Entries.Should().HaveCount(18);
        page.AlwaysShowAllCommands.Should().BeTrue();

        page.Entries[0].State = true;
        page.AlwaysShowAllCommands = false;
        page.Store(preferences);

        preferences.CascadeShellMenuItems.Should().Be("010111000111111111");
        preferences.AlwaysShowAllCommands.Should().BeFalse();
    }

    [Test]
    public void Load_should_leave_items_a_short_setting_has_no_character_for_out_of_the_menu()
    {
        ShellExtensionPageViewModel page = New();

        page.Load(new InMemoryAppPreferences { CascadeShellMenuItems = "01" });

        page.Entries.Select(entry => entry.State).Should().Equal([true, null, .. Enumerable.Repeat<bool?>(false, 16)]);
    }

    [Test]
    public void Next_should_step_through_upstreams_order()
    {
        ShellMenuEntry.Next(true).Should().BeFalse();
        ShellMenuEntry.Next(false).Should().BeNull();
        ShellMenuEntry.Next(null).Should().BeTrue();
    }

    [Test]
    public void Preview_should_list_the_top_level_items_then_the_cascaded_menu()
    {
        ShellExtensionPageViewModel page = New();
        string newLine = Environment.NewLine;

        page.Preview.Should().Be("(no items)");

        page.Entries[0].State = true;
        page.Preview.Should().Be($"GitExt Add files...{newLine}");

        page.Entries[7].State = null;
        page.Entries[15].State = null;
        page.Preview.Should().Be($"GitExt Add files...{newLine}Git Extensions > {newLine}       Commit...{newLine}       Settings{newLine}");
    }

    [Test]
    public void UpdateRegistrationStatus_should_enable_the_group_with_the_files_and_Enable_while_not_registered()
    {
        ShellExtensionPageViewModel page = New();
        _installation.FilesExist = true;

        page.UpdateRegistrationStatus();

        page.FilesExist.Should().BeTrue();
        page.CanRegister.Should().BeTrue();

        _installation.IsRegistered = true;
        page.UpdateRegistrationStatus();
        page.CanRegister.Should().BeFalse();
    }

    [Test]
    public async Task Register_and_Unregister_should_show_the_status_after()
    {
        ShellExtensionPageViewModel page = New();
        _installation.FilesExist = true;

        await page.RegisterCommand.ExecuteAsync(null);
        page.CanRegister.Should().BeFalse();

        await page.UnregisterCommand.ExecuteAsync(null);
        page.CanRegister.Should().BeTrue();
        (_installation.Registrations, _installation.Unregistrations).Should().Be((1, 1));
        _errors.Should().BeEmpty();
    }

    [Test]
    public async Task A_failure_should_be_shown_and_a_declined_prompt_not()
    {
        ShellExtensionPageViewModel page = New();
        _installation.RegisterError = new FileNotFoundException(null, ShellExtensionFiles.ShellEx64Name);

        await page.RegisterCommand.ExecuteAsync(null);
        _installation.RegisterError = new OperationCanceledException();
        await page.RegisterCommand.ExecuteAsync(null);

        _errors.Should().ContainSingle().Which.Should().BeOfType<FileNotFoundException>();
        page.CanRegister.Should().BeTrue();
    }

    [Test]
    public void ShowHelp_should_open_upstreams_manual_section()
    {
        New().ShowHelpCommand.Execute(null);

        _helps.Should().Be(1);
    }

    private ShellExtensionPageViewModel New() => new(_installation, _errors.Add, () => _helps++);

    private static string UpstreamSource(string file, [CallerFilePath] string testFile = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", "..", "src", "app", "GitUI",
            "CommandsDialogs", "SettingsDialog", "Pages", file)).ReplaceLineEndings("\n");
}
