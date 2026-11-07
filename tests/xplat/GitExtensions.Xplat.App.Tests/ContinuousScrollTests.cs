using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream FileViewer's continuous scroll: a wheel turn past the end of a diff, with Alt or upstream's setting, shows the
///  next or previous file of the list.
/// </summary>
internal sealed class ContinuousScrollTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "first.txt"), "one\n");
        File.WriteAllText(Path.Combine(_repo.Path, "second.txt"), "two\n");
        _repo.Run("add", "-A");
        _repo.Run("commit", "-q", "-m", "files");
        File.WriteAllText(Path.Combine(_repo.Path, "first.txt"), "one changed\n");
        File.WriteAllText(Path.Combine(_repo.Path, "second.txt"), "two changed\n");
        TestAppBuilder.Preferences.AutomaticContinuousScrollDelay = 0;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.AutomaticContinuousScroll = false;
        TestAppBuilder.Preferences.AutomaticContinuousScrollDelay = 600;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void A_wheel_turn_past_the_diffs_end_with_Alt_shows_the_next_or_previous_file_of_the_commit_window()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        WaitUntil(() => Find<Button>(window, "CommitDialogButton").IsEnabled);
        Find<Button>(window, "CommitDialogButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        WaitUntil(() => window.OwnedWindows.OfType<CommitWindow>().Any());
        CommitWindow commit = window.OwnedWindows.OfType<CommitWindow>().Single();
        ListBox unstaged = Find<ListBox>(commit, "UnstagedList");
        WaitUntil(() => unstaged.Items.OfType<FileChange>().Count() == 2);
        unstaged.SelectedItem = unstaged.Items.OfType<FileChange>().Single(change => change.Path == "first.txt");
        DiffView diff = Find<DiffView>(commit, "Diff");
        WaitUntil(() => diff.Lines.Any(line => line.Text == "+one changed"));

        Wheel(commit, diff, down: true, RawInputModifiers.None);
        Selected(unstaged).Should().Be("first.txt", "upstream moves without Alt only with its setting on");

        Wheel(commit, diff, down: true, RawInputModifiers.Alt);
        WaitUntil(() => Selected(unstaged) == "second.txt" && diff.Lines.Any(line => line.Text == "+two changed"));

        TestAppBuilder.Preferences.AutomaticContinuousScroll = true;
        Wheel(commit, diff, down: false, RawInputModifiers.None);
        WaitUntil(() => Selected(unstaged) == "first.txt");
        commit.Close();
        window.Close();
    }

    private static string? Selected(ListBox list) => (list.SelectedItem as FileChange)?.Path;

    // A wheel turn over the diff's lines, which are all shown, so the diff is at its top and its bottom.
    private static void Wheel(Window window, DiffView diff, bool down, RawInputModifiers modifiers)
    {
        ListBox lines = diff.FindControl<ListBox>("DiffList")!;
        // Below the viewer's toolbar, which shows over the top lines while the pointer is on the viewer.
        Point point = lines.TranslatePoint(new Point(lines.Bounds.Width / 2, lines.Bounds.Height / 2), window)!.Value;
        window.MouseWheel(point, new Vector(0, down ? -1 : 1), modifiers);
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
