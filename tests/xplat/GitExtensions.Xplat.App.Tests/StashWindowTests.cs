using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

        WaitUntil(() => !window.IsLoadingFiles && Rows(window).Count > 1);
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
        => Find<ListBox>(window, "FilesList").ItemsSource as IReadOnlyList<StashFileRow> ?? [];

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
