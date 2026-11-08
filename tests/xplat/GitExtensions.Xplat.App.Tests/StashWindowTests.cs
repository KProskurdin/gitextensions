using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using Avalonia.VisualTree;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class StashWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "b");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "third");
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_opens_with_its_first_control_focused_not_a_dialog_button()
    {
        StashWindow window = Open(manageStashes: true);

        window.FocusManager.GetFocusedElement().Should().BeSameAs(window.FindControl<ComboBox>("StashesBox"));
        window.Close();
    }

    [AvaloniaTest]
    public void The_window_shows_the_newest_stash_first_and_the_working_directory_in_groups()
    {
        Write("a.txt", "stashed");
        _repo.Run("stash", "push", "-q", "-m", "first stash");
        Write("a.txt", "staged");
        _repo.Run("add", "a.txt");
        Write("c.txt", "untracked");
        StashWindow window = Open(manageStashes: true);

        window.Dialog.Selected!.Name.Should().Be("stash@{0}");
        Rows(window).Select(row => row.Text).Should().Equal("a.txt");
        Find<TextBox>(window, "MessageBox").Text.Should().EndWith("first stash");
        Find<TextBox>(window, "MessageBox").IsReadOnly.Should().BeTrue();
        Find<Button>(window, "DropButton").IsEnabled.Should().BeTrue();
        Find<Button>(window, "StashSelectedButton").IsEnabled.Should().BeFalse();

        Find<ComboBox>(window, "StashesBox").SelectedIndex = 0;

        Find<ProgressBar>(window, "Loading").IsVisible.Should().BeTrue("the files are being read");
        WaitUntil(() => !window.IsLoadingFiles && Rows(window).Count > 1);
        Find<ProgressBar>(window, "Loading").IsVisible.Should().BeFalse();
        Rows(window).Select(row => row.Text).Should().Equal("Commit index", "a.txt", "Working directory", "c.txt");
        Find<Button>(window, "DropButton").IsEnabled.Should().BeFalse();
        Find<TextBox>(window, "MessageBox").IsReadOnly.Should().BeFalse();
        window.Close();
    }

    [AvaloniaTest]
    public void Stash_all_changes_stashes_with_the_typed_message_and_keeps_the_options()
    {
        Write("a.txt", "changed");
        Write("c.txt", "untracked");
        StashWindow window = Open(manageStashes: false);
        Find<TextBox>(window, "MessageBox").Text = "  my work  ";
        Find<CheckBox>(window, "IncludeUntrackedCheck").IsChecked = true;

        Click(window, "StashAllButton");

        WaitUntil(() => window.Dialog.Items.Count == 2 && !window.IsLoadingFiles);
        _repo.Run("stash", "list").Should().Contain("my work");
        File.Exists(Path.Combine(_repo.Path, "c.txt")).Should().BeFalse();
        window.Close();
        TestAppBuilder.Preferences.IncludeUntrackedFilesInManualStash.Should().BeTrue();
    }

    [AvaloniaTest]
    public void The_files_are_upstreams_folder_tree_below_their_group()
    {
        Directory.CreateDirectory(Path.Combine(_repo.Path, "docs"));
        Write("docs/n.txt", "n");
        _repo.Run("add", "docs/n.txt");
        _repo.Run("commit", "-q", "-m", "add notes");
        Write("docs/n.txt", "changed");
        StashWindow window = Open(manageStashes: false);
        WaitUntil(() => Rows(window).Count(row => row.IsFile) == 1);
        ListBox files = Find<ListBox>(window, "FilesList");

        files.Items.OfType<FileTreeRow>().Select(row => row.Text).Should().Equal("docs");
        Dispatcher.UIThread.RunJobs();
        ListBoxItem file = files.GetVisualDescendants().OfType<ListBoxItem>()
            .Single(item => item.DataContext is StashFileRow { IsFile: true });
        FileRows.GetText(file).Should().Be("n.txt", "a file is named below its folder");
        window.Close();
    }

    [AvaloniaTest]
    public void The_lists_toolbar_filters_the_files_and_its_tree_button_lists_them_flat()
    {
        DiffListSortType sorting = TestAppBuilder.Preferences.DiffListSorting;
        TestAppBuilder.Preferences.DiffListSorting = DiffListSortType.FilePath;
        try
        {
            Directory.CreateDirectory(Path.Combine(_repo.Path, "docs"));
            Write("docs/n.txt", "n");
            _repo.Run("add", "docs/n.txt");
            _repo.Run("commit", "-q", "-m", "add notes");
            Write("docs/n.txt", "changed");
            Write("a.txt", "changed");
            StashWindow window = Open(manageStashes: false);
            WaitUntil(() => Rows(window).Count(row => row.IsFile) == 2);
            ListBox files = Find<ListBox>(window, "FilesList");

            Find<FileFilterBox>(window, "FilesFilterBox").Text = @"^docs/";
            Dispatcher.UIThread.RunJobs();
            Rows(window).Where(row => row.IsFile).Select(row => row.Text).Should().Equal("docs/n.txt");

            Find<FileFilterBox>(window, "FilesFilterBox").Text = "";
            Find<ToolSplitButton>(window, "FilesAsTreeButton").RaiseEvent(new RoutedEventArgs(SplitButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            TestAppBuilder.Preferences.DiffListSorting.Should().Be(DiffListSortType.FilePathFlat);
            files.Items.OfType<FileTreeRow>().Should().BeEmpty("the flat list has no folders");
            Rows(window).Count(row => row.IsFile).Should().Be(2);
            window.Close();
        }
        finally
        {
            TestAppBuilder.Preferences.DiffListSorting = sorting;
        }
    }

    [AvaloniaTest]
    public void Stash_selected_changes_stashes_only_the_selected_files()
    {
        Write("a.txt", "changed");
        Write("b.txt", "changed");
        StashWindow window = Open(manageStashes: false);
        WaitUntil(() => Rows(window).Count(row => row.IsFile) == 2);

        Find<ListBox>(window, "FilesList").SelectedItems!.Clear();
        Find<ListBox>(window, "FilesList").SelectedItems!.Add(Rows(window).Single(row => row.Text == "a.txt"));
        Find<Button>(window, "StashSelectedButton").IsEnabled.Should().BeTrue();
        Click(window, "StashSelectedButton");

        WaitUntil(() => window.Dialog.Items.Count == 2 && !window.IsLoadingFiles);
        _repo.Run("diff", "--name-only").Trim().Should().Be("b.txt");
        window.Close();
    }

    [AvaloniaTest]
    public void Drop_asks_first_and_then_shows_the_stash_that_took_its_place()
    {
        Write("a.txt", "older");
        _repo.Run("stash", "push", "-q", "-m", "older stash");
        Write("a.txt", "newer");
        _repo.Run("stash", "push", "-q", "-m", "newer stash");
        StashWindow window = Open(manageStashes: true);

        Click(window, "DropButton");

        WaitUntil(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = window.OwnedWindows.OfType<ConfirmWindow>().Single();
        question.Title.Should().Be("Drop Stash Confirmation");
        Click(question, "ConfirmButton");
        WaitUntil(() => window.Dialog.Items.Count == 2 && !window.IsLoadingFiles);
        window.Dialog.Selected!.Message.Should().EndWith("older stash");
        _repo.Run("stash", "list").Should().NotContain("newer stash");
        window.Close();
    }

    [AvaloniaTest]
    public void Apply_applies_the_selected_stash_and_keeps_it()
    {
        Write("a.txt", "stashed");
        _repo.Run("stash", "push", "-q", "-m", "to apply");
        StashWindow window = Open(manageStashes: true);

        Click(window, "ApplyButton");

        WaitUntil(() => _repo.ReadFile("a.txt") == "stashed");
        _repo.Run("stash", "list").Should().Contain("to apply");
        window.Close();
    }

    [AvaloniaTest]
    public void The_working_directorys_files_get_upstreams_file_menu_for_their_group()
    {
        Write("a.txt", "staged");
        _repo.Run("add", "a.txt");
        Write("b.txt", "changed");
        StashWindow window = Open(manageStashes: false);
        WaitUntil(() => Rows(window).Count(row => row.IsFile) == 2);
        ListBox files = Find<ListBox>(window, "FilesList");
        files.SelectedItems!.Clear();
        files.SelectedItems.Add(Rows(window).Single(row => row.Text == "a.txt"));
        files.SelectedItems.Add(Rows(window).Single(row => row.Text == "b.txt"));
        OpenMenu(files);

        Item(files, "tsmiStageFile").IsVisible.Should().BeTrue("b.txt is a working directory change");
        Item(files, "tsmiUnstageFile").IsVisible.Should().BeTrue("a.txt is staged");
        Item(files, "tsmiShowInFileTree").IsVisible.Should().BeFalse("upstream's FormStash binds no file tree");
        Item(files, "tsmiFilterFileInGrid").IsVisible.Should().BeFalse();
        Item(files, "tsmiResetFileToParent").Header.Should().Be("First: A " + _repo.Run("rev-parse", "--short=8", "HEAD").Trim(),
            "the first selected file, a.txt, is shown between HEAD and the index");

        files.SelectedItems.Clear();
        files.SelectedItems.Add(Rows(window).Single(row => row.Text == "a.txt"));
        OpenMenu(files);
        Item(files, "tsmiUnstageFile").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => _repo.Run("diff", "--cached", "--name-only").Trim().Length == 0);
        WaitUntil(() => !window.IsLoadingFiles && Rows(window).Count(row => row.IsHeader) == 1);
        window.Close();
    }

    [AvaloniaTest]
    public void A_stashs_file_can_be_reset_to_the_stash_from_the_file_menu()
    {
        Write("a.txt", "stashed");
        _repo.Run("stash", "push", "-q", "-m", "to reset");
        StashWindow window = Open(manageStashes: true);
        ListBox files = Find<ListBox>(window, "FilesList");
        OpenMenu(files);

        Item(files, "tsmiStageFile").IsVisible.Should().BeFalse();
        Item(files, "tsmiResetFileToSelected").Header.Should()
            .Be("Second: B " + _repo.Run("rev-parse", "--short=8", "stash@{0}").Trim());
        Item(files, "tsmiResetFileToSelected").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<ResetChangesWindow>().Any());
        Click(window.OwnedWindows.OfType<ResetChangesWindow>().Single(), "ResetButton");

        WaitUntil(() => _repo.ReadFile("a.txt") == "stashed");
        window.Close();
    }

    // The menu updates itself for the selection when it opens, as upstream's ItemContextMenu_Opening.
    private static void OpenMenu(ListBox list)
    {
        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        list.ContextMenu!.Close();
    }

    private static MenuItem Item(ListBox list, string name)
        => list.ContextMenu!.FindControl<MenuItem>(name) ?? throw new InvalidOperationException($"No item {name}");

    private static void Reset()
    {
        TestAppBuilder.Preferences.IncludeUntrackedFilesInManualStash = false;
        TestAppBuilder.Preferences.StashKeepIndex = false;
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    private void Write(string file, string text) => File.WriteAllText(Path.Combine(_repo.Path, file), text);

    private StashWindow Open(bool manageStashes)
    {
        StashWindow window = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences,
            manageStashes);
        window.Show();
        WaitUntil(() => window.StashesLoaded.IsCompleted && !window.IsLoadingFiles);
        return window;
    }

    private static IReadOnlyList<StashFileRow> Rows(StashWindow window)
        => [.. Find<ListBox>(window, "FilesList").ItemsSource?.OfType<StashFileRow>() ?? []];

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name) where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Control {name} not found");

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
