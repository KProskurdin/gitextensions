using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's ShellExtensionSettingsPage, the Visual Studio box of its Appearance page and the Checklist's install and shell
// extension rows: Windows only.
internal sealed class ShellExtensionSettingsTests
{
    private const string DefaultMenuItems = "110111000111111111";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.UseWindowsInstallation(TestAppBuilder.NewInstallation());
        TestAppBuilder.Preferences.CascadeShellMenuItems = DefaultMenuItems;
        TestAppBuilder.Preferences.AlwaysShowAllCommands = false;
        TestAppBuilder.Preferences.ShowCurrentBranchInVisualStudio = true;
    }

    [AvaloniaTest]
    [Platform(Exclude = "Win")]
    public void The_page_and_the_Visual_Studio_box_are_left_out_off_Windows()
    {
        SettingsWindow settings = OpenSettings();

        settings.PageTree[0].Children.Select(node => node.Title).Should().NotContain("Shell extension");
        Find<CheckBox>(settings, "chkShowCurrentBranchInVisualStudio").IsVisible.Should().BeFalse();
        settings.Close();
    }

    [AvaloniaTest]
    [Platform(Include = "Win")]
    public void The_page_shows_where_each_item_goes_steps_through_upstreams_order_and_OK_stores_it()
    {
        TestAppBuilder.Preferences.CascadeShellMenuItems = "0" + DefaultMenuItems[1..];
        SettingsWindow settings = OpenSettings();
        SettingsPageNode node = settings.PageTree[0].Children.Single(child => child.Title == "Shell extension");
        settings.GoToPage(node);
        settings.UpdateLayout();

        List<ShellMenuCheckBox> boxes = [.. Find<ItemsControl>(settings, "_NO_TRANSLATE_chlMenuEntries")
            .GetLogicalDescendants().OfType<ShellMenuCheckBox>()];
        boxes.Should().HaveCount(18);
        boxes.Take(3).Select(box => box.IsChecked).Should().Equal(true, null, true);
        Find<TextBlock>(settings, "labelPreview").Text.Should().StartWith("GitExt Add files...");

        ClickBox(settings, boxes[0]);
        boxes[0].IsChecked.Should().BeFalse("a top level item leaves the menu");
        ClickBox(settings, boxes[0]);
        boxes[0].IsChecked.Should().BeNull("an item out of the menu goes into the cascaded one");
        ClickBox(settings, boxes[1]);
        boxes[1].IsChecked.Should().BeTrue("a cascaded item goes to the top level");
        Find<TextBlock>(settings, "labelPreview").Text.Should().StartWith("GitExt Apply patch...");
        Find<CheckBox>(settings, "cbAlwaysShowAllCommands").IsChecked = true;

        Click(settings, "SaveButton");

        WaitUntil(() => TestAppBuilder.Preferences.CascadeShellMenuItems != "0" + DefaultMenuItems[1..]);
        TestAppBuilder.Preferences.CascadeShellMenuItems.Should().Be("100111000111111111");
        TestAppBuilder.Preferences.AlwaysShowAllCommands.Should().BeTrue();
    }

    [AvaloniaTest]
    [Platform(Include = "Win")]
    public void The_buttons_register_the_extension_once_its_files_are_found()
    {
        InMemoryWindowsInstallation installation = TestAppBuilder.NewInstallation();
        TestAppBuilder.UseWindowsInstallation(installation);
        SettingsWindow settings = OpenSettings();

        Find<StackPanel>(settings, "flpnlExplorerIntegration").IsEnabled.Should().BeFalse("no files, nothing to register");
        settings.Close();

        installation.FilesExist = true;
        settings = OpenSettings();
        Find<StackPanel>(settings, "flpnlExplorerIntegration").IsEnabled.Should().BeTrue();
        Button register = Find<Button>(settings, "RegisterButton");
        register.IsEnabled.Should().BeTrue();

        Click(settings, "RegisterButton");

        WaitUntil(() => !register.IsEnabled);
        installation.IsRegistered.Should().BeTrue();
        Click(settings, "UnregisterButton");
        WaitUntil(() => register.IsEnabled);
        installation.Unregistrations.Should().Be(1);
        settings.Close();
    }

    [AvaloniaTest]
    [Platform(Include = "Win")]
    public void The_Visual_Studio_box_shows_and_stores_upstreams_setting()
    {
        SettingsWindow settings = OpenSettings();
        CheckBox box = Find<CheckBox>(settings, "chkShowCurrentBranchInVisualStudio");
        box.IsVisible.Should().BeTrue();
        box.IsChecked.Should().BeTrue();

        box.IsChecked = false;
        Click(settings, "SaveButton");

        WaitUntil(() => !TestAppBuilder.Preferences.ShowCurrentBranchInVisualStudio);
    }

    [AvaloniaTest]
    [Platform(Include = "Win")]
    public void The_checklist_repairs_the_install_folder_and_registers_the_extension()
    {
        InMemoryWindowsInstallation installation = new()
        {
            InstallDir = TestAppBuilder.AvatarCacheFolder, AppDirectory = TestAppBuilder.TestCurrentDirectory,
            FilesExist = true,
        };
        Directory.CreateDirectory(TestAppBuilder.AvatarCacheFolder);
        TestAppBuilder.UseWindowsInstallation(installation);
        SettingsWindow settings = OpenSettings();
        settings.GoToPage(settings.PageTree[0]);
        WaitUntil(() => settings.ChecklistEntries.Count > 0);

        settings.ChecklistEntries.Where(entry =>
                entry.Item is ChecklistItem.ShellExtension or ChecklistItem.GitExtensionsInstall)
            .Should().Equal(
                new ChecklistEntry(ChecklistItem.ShellExtension, ChecklistState.Unset,
                    "GitExtensionsShellEx32.dll needs to be registered in order to use the shell extensions."),
                new ChecklistEntry(ChecklistItem.GitExtensionsInstall, ChecklistState.Unset,
                    SettingsChecklist.InstallDirFaulty));

        Repair(settings, ChecklistItem.GitExtensionsInstall);
        WaitUntil(() => settings.ChecklistEntries.Any(entry => entry is
            { Item: ChecklistItem.GitExtensionsInstall, State: ChecklistState.Set }));
        Repair(settings, ChecklistItem.ShellExtension);
        WaitUntil(() => settings.ChecklistEntries.Any(entry => entry is
            { Item: ChecklistItem.ShellExtension, State: ChecklistState.Set }));

        installation.InstallDir.Should().Be(TestAppBuilder.TestCurrentDirectory);
        installation.Registrations.Should().Be(1);
        settings.Close();
    }

    private static void Repair(SettingsWindow settings, ChecklistItem item)
    {
        ItemsControl rows = Find<ItemsControl>(settings, "ChecklistGrid");
        WaitUntil(() =>
        {
            settings.UpdateLayout();
            return rows.GetLogicalDescendants().OfType<Button>().Any(button =>
                button.DataContext is ChecklistRow row && row.Item == item &&
                Equals(button.Content, SettingsChecklist.RepairText) && button.IsVisible);
        });
        Click(rows.GetLogicalDescendants().OfType<Button>().First(button =>
            button.DataContext is ChecklistRow row && row.Item == item &&
            Equals(button.Content, SettingsChecklist.RepairText)));
    }

    private static SettingsWindow OpenSettings()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        Click(window, "SettingsMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<SettingsWindow>().Any());
        SettingsWindow settings = window.OwnedWindows.OfType<SettingsWindow>().Single();
        WaitUntil(() => settings.IsGitConfigLoaded);
        return settings;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        UiClicks.Raise(control, control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent);
    }

    private static void Click(Button button) => UiClicks.Click(button);

    // A check box steps through its states in its own click handling, which a raised click event does not reach.
    private static void ClickBox(Window window, CheckBox box)
    {
        window.UpdateLayout();
        Point middle = box.TranslatePoint(new Point(box.Bounds.Width / 2, box.Bounds.Height / 2), window)
                       ?? throw new InvalidOperationException("The check box is not in the window");
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was not met in time.");
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
