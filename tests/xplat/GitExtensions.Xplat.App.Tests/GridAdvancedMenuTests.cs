using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  The grid menu's Advanced submenu (upstream's manipulateCommitToolStripMenuItem): edit and reword through an interactive
///  rebase, and the fixup, squash and amend commits in the commit window.
/// </summary>
internal sealed class GridAdvancedMenuTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "three");
        _repo.Run("commit", "-q", "-am", "third");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(Path.Combine(_repo.Path, ".git", "rebase-merge")))
        {
            _repo.Run("rebase", "--abort");
        }

        _repo.Dispose();
    }

    [AvaloniaTest]
    public void A_fixup_commit_starts_from_upstreams_read_only_message_until_modify_is_clicked()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "second");

        Click(Find<MenuItem>(window, "FixupCommitMenuItem"));

        CommitWindow commit = WaitForCommitWindow(window);
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => message.Text == "fixup! second");
        message.IsEnabled.Should().BeFalse("git finds the commit to fix up by this subject");
        Button modify = Find<Button>(commit, "modifyCommitMessageButton");
        modify.IsVisible.Should().BeTrue();

        modify.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        message.IsEnabled.Should().BeTrue();
        modify.IsVisible.Should().BeFalse();
        commit.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void An_amend_commit_starts_with_the_prefixed_subject_and_the_whole_message()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "second");

        Click(Find<MenuItem>(window, "AmendCommitMenuItem"));

        CommitWindow commit = WaitForCommitWindow(window);
        TextBox message = Find<TextBox>(commit, "CommitMessageBox");
        WaitUntil(() => message.Text?.StartsWith("amend! second", StringComparison.Ordinal) == true);
        message.Text!.ReplaceLineEndings("\n").TrimEnd().Should().Be("amend! second\n\nsecond\n\nbody line");
        message.IsEnabled.Should().BeTrue();
        Find<Button>(commit, "modifyCommitMessageButton").IsVisible.Should().BeFalse();
        commit.Close();
        window.Close();
    }

    [AvaloniaTest]
    public void Edit_commit_stops_a_rebase_at_the_selected_commit()
    {
        MainWindow window = OpenWindow();
        SelectCommit(window, "second");
        string second = _repo.Run("rev-parse", "HEAD~1").Trim();

        Click(Find<MenuItem>(window, "EditCommitMenuItem"));

        WaitUntil(() => Directory.Exists(Path.Combine(_repo.Path, ".git", "rebase-merge")) && !window.IsBusy);
        _repo.Run("rev-parse", "HEAD").Trim().Should().Be(second, "the rebase stops at the commit to edit");
        window.Close();
    }

    [Test]
    public async Task The_macOS_sequence_editor_turns_only_the_first_pick_into_the_command()
    {
        GitOperations operations = new();

        await operations.RunRebaseAsync(_repo.Path,
            RewriteCommit.Arguments(GitExtensions.Extensibility.Git.ObjectId.Parse(_repo.Run("rev-parse", "HEAD~2").Trim()),
                supportRebaseMerges: true),
            editorCommand: null, sequenceEditor: RewriteCommit.SequenceEditor(RewriteCommit.Edit, onMac: true));

        File.ReadAllText(Path.Combine(_repo.Path, ".git", "rebase-merge", "done")).Should().StartWith("e");
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("second", "the rebase stops at the first commit only");
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static void SelectCommit(MainWindow window, string subject)
    {
        ListBox commits = Find<ListBox>(window, "CommitList");
        commits.SelectedItem = commits.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == subject);
    }

    private static CommitWindow WaitForCommitWindow(Window owner)
    {
        WaitUntil(() => owner.OwnedWindows.OfType<CommitWindow>().Any(window => window.IsVisible));
        return owner.OwnedWindows.OfType<CommitWindow>().Single();
    }

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

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
