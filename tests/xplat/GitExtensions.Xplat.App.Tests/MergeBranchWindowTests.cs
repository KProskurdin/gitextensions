using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class MergeBranchWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private string _base = "";

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _base = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("checkout", "-q", "-b", "feature");
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "feature");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "feature work");
        _repo.Run("checkout", "-q", _base);
        Reset();
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_shows_the_current_branch_and_upstreams_defaults()
    {
        MergeBranchWindow merge = Open("feature");

        Find<TextBlock>(merge, "CurrentBranchText").Text.Should().Be(_base);
        Find<ComboBox>(merge, "BranchesBox").Text.Should().Be("feature");
        Find<RadioButton>(merge, "FastForwardRadio").IsChecked.Should().BeTrue();
        Find<Control>(merge, "AdvancedPanel").IsVisible.Should().BeFalse();

        Find<CheckBox>(merge, "AdvancedCheck").IsChecked = true;

        Find<Control>(merge, "AdvancedPanel").IsVisible.Should().BeTrue();
        merge.Close();
    }

    [AvaloniaTest]
    public void A_merge_commit_with_a_specified_message_is_made_and_the_choice_kept()
    {
        MergeBranchWindow merge = Open("feature");
        Find<RadioButton>(merge, "NoFastForwardRadio").IsChecked = true;
        Find<CheckBox>(merge, "AdvancedCheck").IsChecked = true;
        Find<CheckBox>(merge, "AddMergeMessageCheck").IsChecked = true;
        Find<TextBox>(merge, "MergeMessageBox").Text = "Custom merge";

        Click(merge, "MergeButton");

        WaitUntil(() => _repo.Run("log", "-1", "--format=%s").Trim() == "Custom merge");
        _repo.Run("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Should().HaveCount(3);
        TestAppBuilder.MergeSettings.Settings.NoFastForward.Should().BeTrue();
    }

    [AvaloniaTest]
    public void A_merge_that_stops_on_conflicts_offers_the_conflicts_window()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "f.txt"), "base");
        _repo.Run("add", "f.txt");
        _repo.Run("commit", "-q", "-m", "base work");
        MergeBranchWindow merge = Open("feature");

        Click(merge, "MergeButton");

        WaitUntil(() => merge.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = merge.OwnedWindows.OfType<ConfirmWindow>().Single();
        question.Title.Should().Be("Merge conflicts");
        Click(question, "CancelButton");
        WaitUntil(() => !merge.IsVisible);
        File.Exists(Path.Combine(_repo.Path, ".git", "MERGE_HEAD")).Should().BeTrue();
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.DontCommitMerge = false;
        TestAppBuilder.MergeSettings.Settings = new MergeRepositorySettings(false, false, 20);
        TestAppBuilder.Preferences.AlwaysShowAdvOpt = false;
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    private MergeBranchWindow Open(string? branch)
    {
        Window owner = new();
        owner.Show();
        MergeBranchWindow merge = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, branch, (_, run) => run(), () => { });
        _ = merge.ShowDialog<bool>(owner);
        WaitUntil(() => merge.MergeLoaded.IsCompleted);
        return merge;
    }

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
