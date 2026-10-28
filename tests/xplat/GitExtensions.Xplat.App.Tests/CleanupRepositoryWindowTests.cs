using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CleanupRepositoryWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, ".gitignore"), "*.log\n");
        _repo.Run("add", ".gitignore");
        _repo.Run("commit", "-q", "-m", "ignore logs");
        File.WriteAllText(Path.Combine(_repo.Path, "u.txt"), "untracked");
        File.WriteAllText(Path.Combine(_repo.Path, "keep me.txt"), "untracked");
        File.WriteAllText(Path.Combine(_repo.Path, "i.log"), "ignored");
        Directory.CreateDirectory(Path.Combine(_repo.Path, "d"));
        File.WriteAllText(Path.Combine(_repo.Path, "d", "x.txt"), "untracked");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Preview_lists_what_would_go_and_leaves_the_files()
    {
        (Window owner, CleanupRepositoryWindow window) = Open(cleaned: null);
        Find<CheckBox>(window, "checkBoxExcludePathFilter").IsChecked = true;
        Find<TextBox>(window, "textBoxExcludePaths").Text = "keep me.txt";

        Click(window, "Preview");
        WaitUntil(() => window.LastRun.IsCompleted);

        string log = Find<TextBox>(window, "PreviewOutput").Text ?? "";
        log.Should().Contain("Would remove u.txt").And.Contain("Would remove i.log").And.Contain("Would remove d/");
        log.Should().NotContain("keep me.txt");
        File.Exists(Path.Combine(_repo.Path, "u.txt")).Should().BeTrue();
        owner.Close();
    }

    [AvaloniaTest]
    public void Cleanup_asks_then_removes_only_the_chosen_kind()
    {
        bool cleaned = false;
        (Window owner, CleanupRepositoryWindow window) = Open(() => cleaned = true);
        Find<RadioButton>(window, "RemoveIgnored").IsChecked = true;

        Click(window, "Cleanup");
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be("Cleanup");
        question.GetLogicalDescendants().OfType<Button>().Single(button => button.Name == "YesButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => cleaned);
        File.Exists(Path.Combine(_repo.Path, "i.log")).Should().BeFalse();
        File.Exists(Path.Combine(_repo.Path, "u.txt")).Should().BeTrue("only ignored files go");
        Find<TextBox>(window, "PreviewOutput").Text.Should().Contain("Removing i.log");
        owner.Close();
    }

    [AvaloniaTest]
    public void The_path_boxes_follow_their_check_boxes()
    {
        (Window owner, CleanupRepositoryWindow window) = Open(cleaned: null);
        TextBox include = Find<TextBox>(window, "textBoxIncludePaths");
        include.IsEnabled.Should().BeFalse();
        Find<TextBlock>(window, "labelPathHintInclude").IsVisible.Should().BeFalse();

        Find<CheckBox>(window, "checkBoxIncludePathFilter").IsChecked = true;

        include.IsEnabled.Should().BeTrue();
        Find<TextBlock>(window, "labelPathHintInclude").IsVisible.Should().BeTrue();
        window.Title.Should().Be("Clean working directory");
        owner.Close();
    }

    private (Window Owner, CleanupRepositoryWindow Window) Open(Action? cleaned)
    {
        Window owner = new();
        owner.Show();
        CleanupRepositoryWindow window = new(_repo.Path, cleaned);
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        return (owner, window);
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}");

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
