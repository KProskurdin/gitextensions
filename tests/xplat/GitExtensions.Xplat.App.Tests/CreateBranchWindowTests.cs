using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CreateBranchWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup() => _repo = new TestRepository();

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.AutoNormaliseBranchName = true;
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_proposes_the_current_branch_and_shows_upstreams_commit_summary()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        string hash = _repo.Run("rev-parse", "--short=8", "HEAD").Trim();
        (Window owner, CreateBranchWindow window, _) = Open();

        Find<TextBox>(window, "BranchNameBox").Text.Should().Be(head);
        Find<TextBox>(window, "CommitHashBox").Text.Should().Be(hash);
        Find<TextBlock>(window, "CommitCountText").Text.Should().Be("=");
        Find<HeaderedContentControl>(window, "SummaryGroup").Header.Should().Be(hash);
        Find<TextBlock>(window, "MessageText").Text.Should().Be("second");
        Find<TextBlock>(window, "TagsText").Text.Should().Be("n/a");
        Find<TextBlock>(window, "BranchesText").Text.Should().Be(head);
        Find<CheckBox>(window, "CheckoutAfterCreateCheck").IsChecked.Should().BeTrue();
        window.Close();
        owner.Close();
    }

    [AvaloniaTest]
    public void Create_branch_creates_and_checks_out_the_branch_and_closes()
    {
        (Window owner, CreateBranchWindow window, Task<bool> result) = Open();
        Find<TextBox>(window, "BranchNameBox").Text = "feature";

        Click(window, "CreateButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().BeTrue();
        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature");
        owner.Close();
    }

    [AvaloniaTest]
    public void A_branch_at_another_commit_without_checkout_leaves_HEAD_alone()
    {
        string head = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        string first = _repo.Run("rev-parse", "HEAD~1").Trim();
        (Window owner, CreateBranchWindow window, Task<bool> result) = Open(revision: first);
        Find<TextBlock>(window, "MessageText").Text.Should().Be("first");
        Find<TextBox>(window, "BranchNameBox").Text = "old";
        Find<CheckBox>(window, "CheckoutAfterCreateCheck").IsChecked = false;

        Click(window, "CreateButton");

        WaitUntil(() => result.IsCompleted);
        _repo.Run("rev-parse", "old").Trim().Should().Be(first);
        _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be(head);
        window.CheckedOutOtherCommit.Should().BeFalse();
        owner.Close();
    }

    [AvaloniaTest]
    public void An_invalid_name_is_refused_with_upstreams_message_and_the_window_stays_open()
    {
        // Normalising would fix the name before git sees it.
        TestAppBuilder.Preferences.AutoNormaliseBranchName = false;
        (Window owner, CreateBranchWindow window, Task<bool> result) = Open();
        Find<TextBox>(window, "BranchNameBox").Text = "bad..name";

        Click(window, "CreateButton");

        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow message = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        message.Title.Should().Be("Create branch");
        message.Close();
        result.IsCompleted.Should().BeFalse();
        window.Close();
        owner.Close();
    }

    [AvaloniaTest]
    public void A_folder_name_is_proposed_as_given()
    {
        (Window owner, CreateBranchWindow window, _) = Open(namePrefix: "team/");

        Find<TextBox>(window, "BranchNameBox").Text.Should().Be("team/");
        window.Close();
        owner.Close();
    }

    [AvaloniaTest]
    public void A_repository_without_commits_creates_an_orphan()
    {
        string empty = Path.Combine(Path.GetTempPath(), "xplat-app-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            GitProcess.Run(empty, "init", "-q");
            (Window owner, CreateBranchWindow window, Task<bool> result) = Open(path: empty);

            Find<AccessText>(window, "CreateAtLabel").Text.Should().Be(CreateBranchDialog.CreatingOrphanBranch);
            Find<Control>(window, "CommitPicker").IsVisible.Should().BeFalse();
            Find<CheckBox>(window, "CreateOrphanCheck").IsChecked.Should().BeTrue();
            Find<CheckBox>(window, "CreateOrphanCheck").IsEnabled.Should().BeFalse();
            Find<TextBox>(window, "BranchNameBox").Text = "start";

            Click(window, "CreateButton");

            WaitUntil(() => result.IsCompleted);
            GitProcess.Run(empty, "symbolic-ref", "HEAD").Trim().Should().Be("refs/heads/start");
            owner.Close();
        }
        finally
        {
            GitProcess.DeleteFolder(empty);
        }
    }

    private (Window Owner, CreateBranchWindow Window, Task<bool> Result) Open(string? revision = null, string? namePrefix = null,
        string? path = null)
    {
        Window owner = new();
        owner.Show();
        CreateBranchWindow window = new(path ?? _repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, revision, namePrefix);
        Task<bool> result = window.ShowDialog<bool>(owner);
        WaitUntil(() => window.BranchLoaded.IsCompleted);
        return (owner, window, result);
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
