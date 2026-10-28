using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CommitInfoTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        TestAppBuilder.Preferences.CommitInfoOptions = new CommitInfoOptions();
        TestAppBuilder.Preferences.ShowGpgInformation = true;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.CommitInfoOptions = new CommitInfoOptions();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_commit_info_lists_the_tag_message_branches_tags_and_the_tag_the_commit_derives_from()
    {
        _repo.Run("tag", "-a", "v1", "-m", "release one");
        MainWindow window = OpenWindow(selectedIndex: 1);

        WaitUntil(() => RevisionInfo(window).Contains("Derives from"));

        RevisionInfo(window).Split('\n').Should().Equal(
            "Contained in branches:",
            _repo.Run("branch", "--show-current").Trim(),
            "",
            "Contained in tags:",
            "v1",
            "",
            "Derives from no tag");
    }

    [AvaloniaTest]
    public void An_annotated_tag_on_the_commit_shows_its_message()
    {
        _repo.Run("tag", "-a", "v1", "-m", "release one");
        MainWindow window = OpenWindow(selectedIndex: 0);

        WaitUntil(() => RevisionInfo(window).StartsWith("v1: release one"));

        Find<InfoTextBlock>(window, "DetailRevisionInfo").Lines[0][0].Underline.Should().BeTrue();
        RevisionInfo(window).Should().EndWith("Derives from tag: v1");
    }

    [AvaloniaTest]
    public void The_commit_info_menu_turns_the_tags_off_and_keeps_the_choice()
    {
        MainWindow window = OpenWindow(selectedIndex: 0);
        WaitUntil(() => RevisionInfo(window).Contains("Contained in no tag"));

        Click(window, "ShowContainedInTagsMenuItem");

        WaitUntil(() => RevisionInfo(window).Contains("Derives from") && !RevisionInfo(window).Contains("Contained in no tag"));
        TestAppBuilder.Preferences.CommitInfoOptions.Tags.Should().BeFalse();
        RevisionInfo(window).Should().Contain("Contained in branches:");
    }

    [AvaloniaTest]
    public void Copy_commit_info_puts_the_header_and_the_message_on_the_clipboard()
    {
        MainWindow window = OpenWindow(selectedIndex: 0);
        WaitUntil(() => Find<InfoTextBlock>(window, "DetailMessage").PlainText.Length > 0);

        Click(window, "CopyCommitInfoMenuItem");
        Task<string?> text = TopLevel.GetTopLevel(window)!.Clipboard!.TryGetTextAsync();
        WaitUntil(() => text.IsCompleted);

        text.Result.Should().StartWith("Author: Test <test@example.com>")
            .And.Contain($"Commit hash: {_repo.Run("rev-parse", "HEAD").Trim()}")
            .And.EndWith($"second{Environment.NewLine}{Environment.NewLine}body line");
    }

    [AvaloniaTest]
    public void The_GPG_tab_shows_that_the_commit_and_its_tag_are_not_signed()
    {
        _repo.Run("tag", "-a", "v1", "-m", "release one");
        MainWindow window = OpenWindow(selectedIndex: 0);
        WaitUntil(() => Find<TabItem>(window, "GpgInfoTab").IsVisible);

        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "GpgInfoTab");

        WaitUntil(() => Find<TextBox>(window, "CommitGpgText").Text == "Commit is not signed");
        Find<TextBox>(window, "TagGpgText").Text.Should().Be("Tag is not signed");
        Find<TextBox>(window, "TagGpgText").IsVisible.Should().BeTrue();
    }

    [AvaloniaTest]
    public void The_GPG_tab_is_hidden_when_the_setting_is_off()
    {
        TestAppBuilder.Preferences.ShowGpgInformation = false;
        MainWindow window = OpenWindow(selectedIndex: 0);
        WaitUntil(() => Find<InfoTextBlock>(window, "DetailMessage").PlainText.Length > 0);

        Find<TabItem>(window, "GpgInfoTab").IsVisible.Should().BeFalse();
    }

    private static string RevisionInfo(Window window) => Find<InfoTextBlock>(window, "DetailRevisionInfo").PlainText;

    private MainWindow OpenWindow(int selectedIndex)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        Find<ListBox>(window, "CommitList").SelectedIndex = selectedIndex;
        return window;
    }

    private static void Click(Window window, string name)
        => Find<MenuItem>(window, name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
           window.GetLogicalDescendants().OfType<UserControl>().Select(view => view.FindControl<T>(name))
               .FirstOrDefault(control => control is not null) ??
           throw new InvalidOperationException($"Control {name} not found");

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
            Thread.Sleep(20);
        }
    }
}
