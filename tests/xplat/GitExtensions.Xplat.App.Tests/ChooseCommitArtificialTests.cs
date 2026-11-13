using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's <c>FormChooseCommit</c> with <c>showArtificial</c>, as <c>FormDiff</c> opens it: the working directory and the
///  index can be picked too.
/// </summary>
internal sealed class ChooseCommitArtificialTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private bool _showArtificial;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed in the working directory");
        _showArtificial = TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits;
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = true;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = _showArtificial;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_chooser_lists_the_artificial_commits_only_when_asked()
    {
        Rows(new ChooseCommitWindow(_repo.Path, null, TestAppBuilder.Preferences))
            .Should().NotContain(ObjectId.WorkTreeId.ToString());

        Rows(new ChooseCommitWindow(_repo.Path, null, TestAppBuilder.Preferences, showArtificial: true))
            .Should().Contain([ObjectId.WorkTreeId.ToString(), ObjectId.IndexId.ToString()]);
    }

    [AvaloniaTest]
    public void The_chooser_follows_the_grid_setting_that_hides_the_artificial_commits()
    {
        TestAppBuilder.Preferences.RevisionGraphShowArtificialCommits = false;

        Rows(new ChooseCommitWindow(_repo.Path, null, TestAppBuilder.Preferences, showArtificial: true))
            .Should().NotContain(ObjectId.WorkTreeId.ToString());
    }

    [AvaloniaTest]
    public void An_artificial_commit_can_be_preselected()
    {
        Window owner = new();
        owner.Show();
        ChooseCommitWindow choose = new(_repo.Path, ObjectId.IndexId.ToString(), TestAppBuilder.Preferences, showArtificial: true);
        _ = choose.ShowDialog<CommitRow?>(owner);
        WaitUntil(() => choose.CommitsLoaded.IsCompleted);

        ((CommitListItem)choose.FindControl<ListBox>("revisionGrid")!.SelectedItem!).Row.Hash.Should().Be(ObjectId.IndexId.ToString());
        owner.Close();
    }

    [AvaloniaTest]
    public void The_diff_windows_picker_can_compare_a_commit_to_the_working_directory()
    {
        ObjectId head = ObjectId.Parse(_repo.Run("rev-parse", "HEAD").Trim());
        RevisionDiffWindow diff = new(_repo.Path, head, head, "base", "second", TestAppBuilder.Preferences);
        diff.Show();
        ListBox files = diff.FindControl<ListBox>("DiffFiles")!;

        Button pick = diff.FindControl<Button>("btnAnotherSecondCommit")!;
        UiClicks.Raise(pick, Button.ClickEvent);
        WaitUntil(() => diff.OwnedWindows.OfType<ChooseCommitWindow>().SingleOrDefault()?.CommitsLoaded.IsCompleted == true);
        ChooseCommitWindow choose = diff.OwnedWindows.OfType<ChooseCommitWindow>().Single();
        ListBox grid = choose.FindControl<ListBox>("revisionGrid")!;
        grid.SelectedItem = grid.Items.Cast<CommitListItem>().Single(item => item.Row.Hash == ObjectId.WorkTreeId.ToString());
        UiClicks.Raise(choose.FindControl<Button>("btnOK")!, Button.ClickEvent);

        WaitUntil(() => files.Items.OfType<RevisionDiffFile>().Any(file => file.Path == "a.txt"));
        diff.Close();
    }

    private static List<string> Rows(ChooseCommitWindow choose)
    {
        Window owner = new();
        owner.Show();
        _ = choose.ShowDialog<CommitRow?>(owner);
        WaitUntil(() => choose.CommitsLoaded.IsCompleted);
        List<string> rows = [.. choose.FindControl<ListBox>("revisionGrid")!.Items.Cast<CommitListItem>().Select(item => item.Row.Hash)];
        owner.Close();
        return rows;
    }

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
