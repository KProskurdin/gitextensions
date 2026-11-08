using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Logging;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommandLogWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private bool _captureCallStacks;

    [SetUp]
    public void SetUp()
    {
        _captureCallStacks = AppSettings.LogCaptureCallStacks;
        CommandLog.Clear();
        GitModule.GitCommandCache.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        AppSettings.LogCaptureCallStacks = _captureCallStacks;
        GitModule.GitCommandCache.Clear();
    }

    [AvaloniaTest]
    public void The_log_lists_the_commands_oldest_first_and_follows_the_newest()
    {
        CommandLog.LogProcessStart("git", "status", "/repo");
        CommandLog.LogProcessStart("git", "log -1", "/repo");
        CommandLogWindow window = Open();
        ListBox items = Find<ListBox>(window, "LogItems");

        Ours(window).Should().Equal("status", "log -1");
        items.SelectedIndex.Should().Be(window.Entries.Count - 1);
        Find<TextBox>(window, "LogOutput").Text.Should().Contain("log -1");

        CommandLog.LogProcessStart("git", "branch", "/repo");
        WaitUntil(() => Ours(window).Count == 3);
        items.SelectedIndex.Should().Be(window.Entries.Count - 1, "the newest command stays selected");

        items.SelectedIndex = 0;
        CommandLog.LogProcessStart("git", "tag", "/repo");
        WaitUntil(() => Ours(window).Count == 4);
        items.SelectedIndex.Should().Be(0, "an older selection is kept");

        Click(window, "mnuClear");
        WaitUntil(() => Ours(window).Count == 0);
        window.Close();
    }

    [AvaloniaTest]
    public void The_menus_show_the_command_key()
    {
        CommandLogWindow window = Open();

        List<MenuItem> items = [.. window.FindControl<ListBox>("LogItems")!.ContextMenu!.Items.OfType<MenuItem>()];
        items.Single(item => item.Name == "mnuSaveToFile").InputGesture.Should().Be(new KeyGesture(Key.S, Hotkeys.CommandModifier));
        items.Single(item => item.Name == "mnuCopyCommandLine").InputGesture.Should().Be(new KeyGesture(Key.C, Hotkeys.CommandModifier));
        items.Single(item => item.Name == "mnuClear").InputGesture.Should().Be(new KeyGesture(Key.L, Hotkeys.CommandModifier));
        window.Close();
    }

    [AvaloniaTest]
    public void The_cache_tab_shows_a_cached_command_with_its_invisible_characters()
    {
        GitModule.GitCommandCache.Add("-c color.ui=never rev-parse HEAD", "abc\n", "");
        CommandLogWindow window = Open();

        Find<TabControl>(window, "TabControl").SelectedItem = Find<TabItem>(window, "tabPageCommandCache");
        WaitUntil(() => window.CachedCommands.Count == 1);
        Find<ListBox>(window, "CommandCacheItems").Items.Cast<string>().Should().Equal("rev-parse HEAD");
        Find<TextBox>(window, "commandCacheOutput").Text.Should()
            .Be(CommandLogDialog.CacheDetail("-c color.ui=never rev-parse HEAD", "abc\n", ""));

        Click(window, "tsmiClearCache");
        window.CachedCommands.Should().BeEmpty();
        window.Close();
    }

    [AvaloniaTest]
    public void The_check_boxes_set_word_wrap_always_on_top_and_call_stack_capture()
    {
        CommandLogWindow window = Open();
        Find<TextBox>(window, "LogOutput").TextWrapping.Should().Be(TextWrapping.Wrap);

        Find<CheckBox>(window, "chkWordWrap").IsChecked = false;
        Find<CheckBox>(window, "chkAlwaysOnTop").IsChecked = true;
        Find<CheckBox>(window, "chkCaptureCallStacks").IsChecked = true;

        Find<TextBox>(window, "LogOutput").TextWrapping.Should().Be(TextWrapping.NoWrap);
        Find<TextBox>(window, "commandCacheOutput").TextWrapping.Should().Be(TextWrapping.NoWrap);
        window.Topmost.Should().BeTrue();
        AppSettings.LogCaptureCallStacks.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public void SaveLines_uses_a_tab_or_the_list_separator_for_csv()
    {
        CommandLog.LogProcessStart("git", "status", "/repo");

        CommandLogDialog.SaveLines("log.txt", CommandLog.Commands).Single().Should().Contain("\tgit\tstatus\t/repo");
        string separator = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ListSeparator;
        CommandLogDialog.SaveLines("log.csv", CommandLog.Commands).Single().Should()
            .Contain($"{separator}git{separator}status{separator}/repo");
    }

    private static CommandLogWindow Open()
    {
        Window owner = new();
        owner.Show();
        CommandLogWindow.ShowFor(owner);
        CommandLogWindow window = owner.OwnedWindows.OfType<CommandLogWindow>().Single();
        WaitUntil(() => window.IsVisible);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    // The log is process-wide: other tests' git calls may be in it too.
    private static List<string> Ours(CommandLogWindow window)
        => [.. window.Entries.Where(entry => entry.WorkingDir == "/repo").Select(entry => entry.Arguments)];

    private static void Click(Window window, string name)
        => Find<MenuItem>(window, name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name)
           ?? window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
           ?? throw new InvalidOperationException($"No control named {name}");

    private static void WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out waiting for the UI");
            }

            Dispatcher.UIThread.RunJobs();
        }
    }
}
