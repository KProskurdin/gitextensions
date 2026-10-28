using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class FileHistoryWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();

        // a.txt ("first", "second") is renamed to b.txt, which "third" changes.
        _repo.Run("mv", "a.txt", "b.txt");
        _repo.Run("commit", "-q", "-m", "renamed");
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "three");
        _repo.Run("commit", "-q", "-am", "third");
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_history_follows_the_files_renames_and_shows_the_diff_of_the_selected_commit()
    {
        FileHistoryWindow history = Open("b.txt");

        Subjects(history).Should().Equal("third", "renamed", "second", "first");
        history.Title.Should().StartWith("File History - b.txt - ");
        Find<TabControl>(history, "Tabs").SelectedItem.Should().Be(Find<TabItem>(history, "DiffTab"));
        DiffView diff = Find<DiffView>(history, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+three"));

        Select(history, "second");

        history.Title.Should().StartWith("File History - b.txt (a.txt) - ");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+two"));
    }

    [AvaloniaTest]
    public void The_menu_turns_off_following_renames_and_keeps_the_choice()
    {
        FileHistoryWindow history = Open("b.txt");

        Click(history, "FollowRenamesMenuItem");

        WaitUntil(() => Subjects(history).SequenceEqual(["third", "renamed"]));
        TestAppBuilder.Preferences.FileHistory.FollowRenames.Should().BeFalse();
        Find<MenuItem>(history, "FollowExactRenamesMenuItem").IsEnabled.Should().BeFalse();
    }

    [AvaloniaTest]
    public void Blame_opens_on_its_tab_and_a_line_selects_its_commit()
    {
        FileHistoryWindow history = Open("b.txt", showBlame: true);

        Find<TabControl>(history, "Tabs").SelectedItem.Should().Be(Find<TabItem>(history, "BlameTab"));
        BlameView blame = Find<BlameView>(history, "Blame");
        WaitUntil(() => blame.Lines.Count == 1);
        Select(history, "second");
        WaitUntil(() => history.Shown.IsCompleted && blame.Lines.Count == 1 && blame.Lines[0].Content == "two");

        ListBox lines = blame.FindControl<ListBox>("BlameList")!;
        lines.SelectedIndex = 0;
        lines.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        WaitUntil(() => history.Commits.Selected?.Subject == "second");
    }

    [AvaloniaTest]
    public void Without_load_on_show_the_history_waits_for_load()
    {
        TestAppBuilder.Preferences.FileHistory =
            new FileHistoryOptions(LoadHistoryOnShow: false, LoadBlameOnShow: false);

        FileHistoryWindow history = Open("b.txt");

        Find<ListBox>(history, "HistoryList").IsVisible.Should().BeFalse();
        Click(history, "LoadButton");
        WaitUntil(() => Find<ListBox>(history, "HistoryList").IsVisible && Subjects(history).Count == 4);
    }

    [AvaloniaTest]
    public void A_commit_without_the_file_has_only_the_commit_tab()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "c.txt"), "c");
        _repo.Run("add", "c.txt");
        _repo.Run("commit", "-q", "-m", "add c");
        _repo.Run("rm", "-q", "c.txt");
        _repo.Run("commit", "-q", "-m", "remove c");

        FileHistoryWindow history = Open("c.txt");

        WaitUntil(() => history.Shown.IsCompleted && !Find<TabItem>(history, "DiffTab").IsVisible);
        Find<TabControl>(history, "Tabs").SelectedItem.Should().Be(Find<TabItem>(history, "CommitTab"));
        Find<TextBlock>(history, "CommitTabText").Text.Should()
            .Be("Commit - Git could not identify the file \"c.txt\"");
        WaitUntil(() => Find<CommitDiffView>(history, "CommitDiffPanel").Info.Details?.Message.Contains("remove c")
                        == true);
    }

    [AvaloniaTest]
    public void Double_click_on_a_commit_opens_it_in_upstreams_commit_diff_window()
    {
        FileHistoryWindow history = Open("a.txt");

        Find<ListBox>(history, "HistoryList").RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, null!));

        WaitUntil(() => history.OwnedWindows.OfType<CommitDiffWindow>().Any());
        CommitDiffWindow diff = history.OwnedWindows.OfType<CommitDiffWindow>().Single();
        WaitUntil(() => diff.RevisionLoaded.IsCompleted);
        diff.Title.Should().StartWith("Diff - ");
        diff.Close();
    }

    private static void Reset() => TestAppBuilder.Preferences.FileHistory = new FileHistoryOptions();

    private FileHistoryWindow Open(string file, bool showBlame = false)
    {
        Window owner = new();
        owner.Show();
        FileHistoryWindow history = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, file, revision: null, showBlame, (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);
        history.Show(owner);
        WaitUntil(() => history.HistoryLoaded.IsCompleted);
        WaitUntil(() => history.Commits.Selected is not null || !Find<ListBox>(history, "HistoryList").IsVisible);
        WaitUntil(() => history.Shown.IsCompleted);
        return history;
    }

    private static IReadOnlyList<string> Subjects(FileHistoryWindow history)
        => [.. history.Commits.VisibleRows.Select(item => item.Row.Subject)];

    private static void Select(FileHistoryWindow history, string subject)
    {
        ListBox list = Find<ListBox>(history, "HistoryList");
        list.SelectedItem = history.Commits.VisibleRows.Single(item => item.Row.Subject == subject);
        WaitUntil(() => history.Commits.Selected?.Subject == subject && history.Shown.IsCompleted);
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control switch
        {
            MenuItem => MenuItem.ClickEvent,
            SplitButton => SplitButton.ClickEvent,
            _ => Button.ClickEvent,
        }));
    }

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ??
           window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name) ??
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
