using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's "Find file" (<see cref="SearchWindow"/>): the file list menu's item and the browse window's
///  FindFileInSelectedCommit hotkey.
/// </summary>
internal sealed class FindFileWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        Directory.CreateDirectory(Path.Combine(_repo.Path, "docs"));
        File.WriteAllText(Path.Combine(_repo.Path, "docs", "guide.md"), "g");
        File.WriteAllText(Path.Combine(_repo.Path, "notes.txt"), "n");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "add files");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public async Task The_search_window_lists_the_matches_and_takes_the_one_selected_with_the_arrow_keys()
    {
        Window owner = new();
        owner.Show();
        SearchWindow search = new(text => new[] { "alpha.txt", "beta.txt", "gamma.md" }.Where(name => name.Contains(text)));
        Task<string?> shown = search.ShowAsync(owner);
        Dispatcher.UIThread.RunJobs();
        TextBox box = Find<TextBox>(search, "txtSearchBox");
        ListBox matches = Find<ListBox>(search, "listBoxSearchResult");

        Type(search, box, ".txt");

        matches.IsVisible.Should().BeTrue();
        matches.Items.Cast<string>().Should().Equal("alpha.txt", "beta.txt");
        matches.SelectedItem.Should().Be("alpha.txt", "upstream selects the first match");
        search.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, keySymbol: null);
        search.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        WaitUntil(() => shown.IsCompleted);
        (await shown).Should().Be("beta.txt");
        owner.Close();
    }

    [AvaloniaTest]
    public async Task Escape_closes_the_search_window_without_a_file()
    {
        Window owner = new();
        owner.Show();
        SearchWindow search = new(_ => ["a.txt"]);
        Task<string?> shown = search.ShowAsync(owner);
        Dispatcher.UIThread.RunJobs();
        Type(search, Find<TextBox>(search, "txtSearchBox"), "a");

        search.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);

        WaitUntil(() => shown.IsCompleted);
        (await shown).Should().BeNull();
        owner.Close();
    }

    [AvaloniaTest]
    public void Find_file_in_the_diff_tabs_menu_selects_the_file_taken()
    {
        MainWindow window = OpenWindow();
        ListBox files = SelectCommitFile(window, "add files", "notes.txt");
        files.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        files.ContextMenu!.Close();
        MenuItem findFile = Find<MenuItem>(files.ContextMenu, "tsmiFindFile");
        findFile.IsVisible.Should().BeTrue();

        findFile.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        SearchWindow search = WaitForSearchWindow(window);
        Type(search, Find<TextBox>(search, "txtSearchBox"), "GUIDE");
        search.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        WaitUntil(() => files.SelectedItems!.OfType<CommitFile>().Select(file => file.Path).SequenceEqual(["docs/guide.md"]));
        window.Close();
    }

    [AvaloniaTest]
    public void Ctrl_Shift_F_finds_a_file_of_the_selected_commit_in_the_file_tree()
    {
        MainWindow window = OpenWindow();
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.Focus();

        window.KeyPress(Key.F, TestKeys.Command | RawInputModifiers.Shift, PhysicalKey.F, keySymbol: null);

        SearchWindow search = WaitForSearchWindow(window);
        Find<TabControl>(window, "DetailTabs").SelectedItem.Should().Be(Find<TabItem>(window, "FileTreeTab"));
        Type(search, Find<TextBox>(search, "txtSearchBox"), "guide");
        search.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
        TreeView tree = Find<TreeView>(window, "FileTreeView");
        WaitUntil(() => tree.SelectedItem is FileTreeNode { Path: "docs/guide.md" });
        window.Close();
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static ListBox SelectCommitFile(MainWindow window, string subject, string file)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        WaitUntil(() => commits.Items.Cast<CommitListItem>().Any(item => item.Row.Subject == subject));
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().First(item => item.Row.Subject == subject);
        ListBox files = Find<ListBox>(window, "CommitFilesList");
        WaitUntil(() => files.Items.OfType<CommitFile>().Any(item => item.Path == file));
        files.SelectedItem = files.Items.OfType<CommitFile>().First(item => item.Path == file);
        Dispatcher.UIThread.RunJobs();
        return files;
    }

    private static SearchWindow WaitForSearchWindow(Window owner)
    {
        WaitUntil(() => owner.OwnedWindows.OfType<SearchWindow>().Any());
        return owner.OwnedWindows.OfType<SearchWindow>().Single();
    }

    // Types the text and waits for the matches, which upstream's control reads off the UI thread.
    private static void Type(SearchWindow search, TextBox box, string text)
    {
        box.Focus();
        box.Text = text;
        Dispatcher.UIThread.RunJobs();
        WaitUntil(() => search.Searched.IsCompleted);
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Control root, string name)
        where T : Control
        => root.FindControl<T>(name)
           ?? root.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
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
            Thread.Sleep(10);
        }
    }
}
