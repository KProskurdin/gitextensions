using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream BlameControl's menu in the shared <see cref="BlameView"/>, in the file history (its grid is filtered by path), in
///  the blame window (no grid) and in the File tree tab.
/// </summary>
internal sealed class BlameMenuTests
{
    private const string BlamedFile = "f.txt";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();

        // The file's commits: "base", "insert" adds a first line and changes the last, "other" (another file) is not in the
        // file's history, and "change" changes the third line.
        Commit("base", BlamedFile, "l1\nl2\nl3\nl4\n");
        Commit("insert", BlamedFile, "new\nl1\nl2\nl3\nl4x\n");
        Commit("other", "other.txt", "o\n");
        Commit("change", BlamedFile, "new\nl1\nchanged\nl3\nl4x\n");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Enter_on_a_line_selects_the_commit_that_last_changed_it_and_goes_to_the_line_there()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        Lines(blame).SelectedIndex = 3;

        Lines(blame).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        WaitUntil(() => history.Commits.Selected?.Subject == "base" && blame.Lines.Count == 4);
        Lines(blame).SelectedIndex.Should().Be(2, "the line is the third of that commit");
        SelectedContent(blame).Should().Be("l3");
        history.Close();
    }

    [AvaloniaTest]
    public void Blame_this_revision_selects_the_lines_commit_and_is_disabled_without_a_row_in_the_grid()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        Lines(blame).SelectedIndex = 0;
        OpenMenu(blame);
        Item(blame, "BlameRevisionMenuItem").IsEnabled.Should().BeTrue();

        Click(Item(blame, "BlameRevisionMenuItem"));

        WaitUntil(() => history.Commits.Selected?.Subject == "insert" && blame.Lines is [_, _, { Content: "l2" }, ..]);
        SelectedContent(blame).Should().Be("new");
        history.Close();
    }

    [AvaloniaTest]
    public void Blame_previous_revision_goes_to_the_real_parent_on_the_line_it_had_there()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        Select(history, "insert");
        WaitUntil(() => blame.Lines is [{ Content: "new" }, ..]);
        Lines(blame).SelectedIndex = 4;
        SelectedContent(blame).Should().Be("l4x");
        OpenMenu(blame);
        MenuItem previous = Item(blame, "BlamePreviousRevisionMenuItem");
        WaitUntil(() => previous.IsEnabled);
        previous.Header.Should().Be("_Blame previous revision");

        Click(previous);

        WaitUntil(() => history.Commits.Selected?.Subject == "base" && blame.Lines.Count == 4);
        SelectedContent(blame).Should().Be("l4", "the line was the fourth of the parent, before the insert");
        history.Close();
    }

    [AvaloniaTest]
    public void Blame_previous_revision_says_visible_when_the_grid_does_not_show_the_real_parent()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        Lines(blame).SelectedIndex = 2;
        SelectedContent(blame).Should().Be("changed");
        OpenMenu(blame);
        MenuItem previous = Item(blame, "BlamePreviousRevisionMenuItem");

        WaitUntil(() => previous.IsEnabled);

        previous.Header.Should().Be("_Blame previous visible revision", "the real parent is \"other\", another file's");
        Click(previous);
        WaitUntil(() => history.Commits.Selected?.Subject == "insert" && blame.Lines is [_, _, { Content: "l2" }, ..]);
        SelectedContent(blame).Should().Be("l2", "the parent's third line");
        history.Close();
    }

    [AvaloniaTest]
    public void Blame_previous_revision_is_disabled_for_the_files_first_commit()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        Select(history, "base");
        WaitUntil(() => blame.Lines is [{ Content: "l1" }, ..]);
        Lines(blame).SelectedIndex = 0;
        OpenMenu(blame);
        MenuItem previous = Item(blame, "BlamePreviousRevisionMenuItem");

        WaitUntil(() => previous.Header as string == "_Blame previous visible revision");

        previous.IsEnabled.Should().BeFalse("its parents are not in the file's history");
        Item(blame, "BlameRevisionMenuItem").IsEnabled.Should().BeTrue();
        history.Close();
    }

    [AvaloniaTest]
    public async Task The_copy_items_copy_the_lines_commit()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        string insert = _repo.Run("log", "--format=%H", "--grep=insert").Trim();
        Lines(blame).SelectedIndex = 0;
        OpenMenu(blame);

        Click(Item(blame, "CommitHashMenuItem"));
        (await ClipboardTextAsync(history)).Should().Be(insert);

        Click(Item(blame, "CommitMessageMenuItem"));
        (await ClipboardTextAsync(history)).Should().Be("insert");

        Click(Item(blame, "AllCommitInfoMenuItem"));
        (await ClipboardTextAsync(history)).Should().Contain("Author: Test").And.Contain($"Commit hash: {insert[..8]}")
            .And.Contain("Summary: insert").And.Contain($"FileName: {BlamedFile}");
        history.Close();
    }

    [AvaloniaTest]
    public void Show_changes_opens_the_lines_commit()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        Lines(blame).SelectedIndex = 0;
        OpenMenu(blame);

        Click(Item(blame, "ShowChangesMenuItem"));

        history.OwnedWindows.OfType<CommitDiffWindow>().Should().ContainSingle();
        history.Close();
    }

    [AvaloniaTest]
    public void Without_a_grid_the_blame_items_are_disabled_as_upstreams_blame_window()
    {
        BlameWindow window = new(_repo.Path, "HEAD", BlamedFile);
        window.Show();
        BlameView blame = Find<BlameView>(window, "Blame");
        WaitUntil(() => blame.Lines.Count == 5);
        Lines(blame).SelectedIndex = 0;

        OpenMenu(blame);

        Item(blame, "BlameRevisionMenuItem").IsEnabled.Should().BeFalse();
        Item(blame, "BlamePreviousRevisionMenuItem").IsEnabled.Should().BeFalse();
        Item(blame, "ShowChangesMenuItem").IsEnabled.Should().BeTrue();
        Item(blame, "CopyToClipboardMenuItem").IsEnabled.Should().BeTrue();
        window.Close();
    }

    [AvaloniaTest]
    public void In_the_file_tree_blame_this_revision_selects_the_commit_and_the_file_under_its_old_name()
    {
        _repo.Run("mv", BlamedFile, "renamed.txt");
        _repo.Run("commit", "-q", "-m", "rename");
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        Find<TabControl>(window, "DetailTabs").SelectedItem = Find<TabItem>(window, "FileTreeTab");
        TreeView tree = Find<TreeView>(window, "FileTreeView");
        WaitUntil(() => tree.ItemCount > 0);
        tree.SelectedItem = tree.Items.OfType<FileTreeNode>().Single(node => node.Path == "renamed.txt");
        Dispatcher.UIThread.RunJobs();
        tree.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        tree.ContextMenu!.Close();
        Click(Find<MenuItem>(tree.ContextMenu!, "tsmiBlame"));
        BlameView blame = Find<BlameView>(window, "FileTreeBlame");
        WaitUntil(() => blame.IsVisible && blame.Lines.Count == 5);
        Lines(blame).SelectedIndex = 3;

        OpenMenu(blame);
        Click(Item(blame, "BlameRevisionMenuItem"));

        WaitUntil(() => tree.SelectedItem is FileTreeNode { Path: BlamedFile } && blame.Lines.Count == 4);
        SelectedContent(blame).Should().Be("l3");
        window.SelectedRevisions.Should().ContainSingle().Which.Subject.Should().Be("base");
        window.Close();
    }

    [AvaloniaTest]
    public void The_commit_information_shows_the_blamed_commit_then_the_selected_lines()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        WaitUntil(() => blame.Info.Details?.Hash == HashOf("change"));

        Lines(blame).SelectedIndex = 1;

        WaitUntil(() => blame.Info.Details?.Hash == HashOf("base"));
        Lines(blame).SelectedIndex = 0;
        WaitUntil(() => blame.Info.Details?.Hash == HashOf("insert"));
        history.Close();
    }

    [AvaloniaTest]
    public void The_lines_of_the_commit_under_the_mouse_are_highlighted()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        ListBox lines = Lines(blame);

        // Tall enough for every line to have its container.
        history.Height = 3000;
        WaitUntil(() => lines.ContainerFromIndex(4) is not null);
        Control first = lines.ContainerFromIndex(1)!;
        Point point = first.TranslatePoint(new Point(5, first.Bounds.Height / 2), history)!.Value;

        history.MouseMove(point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Highlighted(lines).Should().Equal(1, 3);

        Control other = lines.ContainerFromIndex(0)!;
        history.MouseMove(other.TranslatePoint(new Point(5, other.Bounds.Height / 2), history)!.Value, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Highlighted(lines).Should().Equal(0, 4);
        history.Close();
    }

    [AvaloniaTest]
    public void A_gutter_cell_has_upstreams_tooltip_on_every_line()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        BlameRow row = Lines(blame).Items.OfType<BlameRow>().ElementAt(4);

        row.AuthorLine.Should().NotBeEmpty();
        row.CommitTooltip.Should().Contain("Summary: insert").And.Contain("Author: Test");
        history.Close();
    }

    [AvaloniaTest]
    public void Find_selects_each_lines_occurrence_around_the_end_and_says_when_there_is_none()
    {
        (FileHistoryWindow history, BlameView blame) = OpenHistory();
        ListBox lines = Lines(blame);
        lines.SelectedIndex = 0;
        lines.ContainerFromIndex(0)!.Focus();
        KeyGesture find = Hotkeys.Diff.GestureFor(DiffCommand.Find)!;
        history.KeyPress(find.Key, TestKeys.Raw(find.KeyModifiers), PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();

        FindWindow window = blame.FindWindow!;
        window.IsVisible.Should().BeTrue();
        window.FindControl<Button>("btnHighlightAll")!.IsVisible.Should().BeFalse("whole lines are selected, nothing can be marked");
        string wanted = SelectedContent(blame)!;
        int other = Enumerable.Range(1, lines.ItemCount - 1).First(index => ((BlameRow)lines.Items[index]!).Content != wanted);
        window.LookFor = ((BlameRow)lines.Items[other]!).Content;
        window.FindControl<Button>("btnFindNext")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => lines.SelectedIndex == other);

        window.LookFor = "nothing-like-this";
        window.FindControl<Button>("btnFindNext")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        window.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        lines.SelectedIndex.Should().Be(other);
        history.Close();
    }

    [Test]
    public async Task Blame_reads_a_files_lines_in_the_repositorys_encoding_and_the_authors_in_the_logs()
    {
        _repo.Run("config", "i18n.filesencoding", "iso-8859-1");
        _repo.Run("config", "user.name", "Zoë");
        File.WriteAllBytes(Path.Combine(_repo.Path, "latin.txt"), System.Text.Encoding.Latin1.GetBytes("café\n"));
        _repo.Run("add", "latin.txt");
        _repo.Run("commit", "-q", "-m", "latin");

        BlameLine line = (await new GitCommitHistory().LoadBlameAsync(_repo.Path, HashOf("latin"), "latin.txt")).Single();

        line.Content.Should().Be("café");
        line.Author.Should().Be("Zoë");
    }

    private (FileHistoryWindow History, BlameView Blame) OpenHistory()
    {
        Window owner = new();
        owner.Show();
        FileHistoryWindow history = new(_repo.Path, TestAppBuilder.Preferences, BlamedFile, revision: null, showBlame: true,
            (_, _) => Task.CompletedTask, (_, _) => Task.CompletedTask);
        history.Show(owner);
        BlameView blame = Find<BlameView>(history, "Blame");
        WaitUntil(() => history.HistoryLoaded.IsCompleted && history.Shown.IsCompleted && blame.Lines.Count == 5);
        return (history, blame);
    }

    private string HashOf(string subject) => _repo.Run("log", "--format=%H", $"--grep={subject}").Trim();

    private void Commit(string subject, string file, string content)
    {
        File.WriteAllText(Path.Combine(_repo.Path, file), content);
        _repo.Run("add", file);
        _repo.Run("commit", "-q", "-m", subject);
    }

    private static void Select(FileHistoryWindow history, string subject)
    {
        Find<ListBox>(history, "HistoryList").SelectedItem =
            history.Commits.VisibleRows.Single(item => item.Row.Subject == subject);
        WaitUntil(() => history.Commits.Selected?.Subject == subject && history.Shown.IsCompleted);
    }

    private static ListBox Lines(BlameView blame) => blame.FindControl<ListBox>("BlameList")!;

    private static IReadOnlyList<int> Highlighted(ListBox lines)
        => [.. Enumerable.Range(0, lines.ItemCount).Where(index => lines.ContainerFromIndex(index) is { } container
            && container.Classes.Contains("sameCommit"))];

    private static string? SelectedContent(BlameView blame) => (Lines(blame).SelectedItem as BlameRow)?.Content;

    private static MenuItem Item(BlameView blame, string name)
        => blame.FindControl<MenuItem>(name) ?? throw new InvalidOperationException($"No menu item {name}");

    private static void OpenMenu(BlameView blame)
    {
        ListBox lines = Lines(blame);
        lines.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        lines.ContextMenu!.Close();
    }

    private static async Task<string?> ClipboardTextAsync(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        Task<string?> text = TopLevel.GetTopLevel(window)!.Clipboard!.TryGetTextAsync();
        WaitUntil(() => text.IsCompleted);
        return await text;
    }

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static T Find<T>(Control root, string name)
        where T : Control
        => root.FindControl<T>(name)
           ?? root.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
           ?? throw new InvalidOperationException($"No control named {name}");

    private static void WaitUntil(Func<bool> condition, string because = "")
    {
        DateTime deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting for the UI {because}");
            }

            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }
}
