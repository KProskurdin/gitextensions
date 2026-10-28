using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class PullWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    [TearDown]
    public void TearDown() => TestAppBuilder.Preferences.AutoStash = false;

    [AvaloniaTest]
    public void The_window_shows_upstreams_groups_and_follows_the_chosen_action()
    {
        PullWindow pull = NewWindow(new PullWindowGit(_ => Task.FromResult<string?>("origin"),
            (_, _, _) => Task.FromResult(false)));
        pull.Show();

        pull.GetLogicalDescendants().OfType<HeaderedContentControl>().Select(group => group.Header).Should()
            .Equal("Pull from", "Branch", "Merge options", "Tag options");
        pull.Title.Should().Be("Pull (C:/repo/)");
        Find<TextBox>(pull, "LocalBranchBox").Text.Should().Be("main");
        Find<TextBox>(pull, "LocalBranchBox").IsEnabled.Should().BeFalse();
        Find<HelpImageView>(pull, "HelpImage").Image1.Should().NotBeNull();
        Find<CheckBox>(pull, "PruneCheck").IsEnabled.Should().BeFalse();

        Find<RadioButton>(pull, "FetchRadio").IsChecked = true;

        pull.Title.Should().Be("Fetch (C:/repo/)");
        Find<AccessText>(pull, "PullButtonText").Text.Should().Be("_Fetch");
        Find<TextBox>(pull, "LocalBranchBox").IsEnabled.Should().BeTrue();
        Find<CheckBox>(pull, "PruneCheck").IsEnabled.Should().BeTrue();
        Find<HelpImageView>(pull, "HelpImage").IsOnHoverShowImage2.Should().BeFalse();
        pull.Close();
    }

    [AvaloniaTest]
    public void The_window_opens_with_the_remotes_box_focused_as_upstreams_FormPullLoad()
    {
        PullWindow pull = NewWindow(new PullWindowGit(_ => Task.FromResult<string?>("origin"),
            (_, _, _) => Task.FromResult(false)));

        pull.Show();
        Dispatcher.UIThread.RunJobs();

        pull.FocusManager.GetFocusedElement().Should().BeSameAs(Find<ComboBox>(pull, "RemotesBox"));
        pull.Close();
    }

    [AvaloniaTest]
    public void Pull_closes_with_upstreams_request_and_keeps_auto_stash()
    {
        Window owner = new();
        owner.Show();
        PullWindow pull = NewWindow(new PullWindowGit(_ => Task.FromResult<string?>("origin"),
            (_, _, _) => Task.FromResult(false)));
        Task<PullRequest?> result = pull.ShowDialog<PullRequest?>(owner);
        Find<RadioButton>(pull, "NoTagsRadio").IsChecked = true;
        Find<CheckBox>(pull, "AutoStashCheck").IsChecked = true;

        Click(pull, "PullButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().Be(new PullRequest("origin", "", PullAction.Merge, Prune: false, AutoStash: true,
            LocalBranch: "", FetchTags: false));
        TestAppBuilder.Preferences.AutoStash.Should().BeTrue();
        TestAppBuilder.Preferences.FormPullAction.Should().Be(GitPullAction.Merge);
        owner.Close();
    }

    [AvaloniaTest]
    public void A_rebase_of_a_merge_commit_is_asked_first()
    {
        Window owner = new();
        owner.Show();
        PullWindow pull = NewWindow(new PullWindowGit(_ => Task.FromResult<string?>("origin"),
            (_, _, _) => Task.FromResult(true)));
        Task<PullRequest?> result = pull.ShowDialog<PullRequest?>(owner);
        Find<RadioButton>(pull, "RebaseRadio").IsChecked = true;

        Click(pull, "PullButton");

        WaitUntil(() => pull.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = pull.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be(PullDialog.RebaseMergeQuestionCaption);
        Click(question, "YesButton");
        WaitUntil(() => result.IsCompleted);
        result.Result!.Action.Should().Be(PullAction.Rebase);
        owner.Close();
    }

    private static PullWindow NewWindow(PullWindowGit git)
        => new(new PullDialog(["origin", "upstream"], "main", GitPullAction.None, GitPullAction.Merge, null, "origin"),
            TestAppBuilder.Preferences, "C:/repo/", ["origin/main", "origin/topic"], ["main"], isShallow: false, git);

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
