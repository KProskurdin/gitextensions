using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's auto stash around a pull (<c>FormPull.PullChanges</c>): stash, pull, then the question before the pop.
/// </summary>
internal sealed class PullAutoStashFlowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private string _remote = null!;
    private string _other = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _remote = Path.Combine(Path.GetTempPath(), "xplat-remote-" + Guid.NewGuid().ToString("N"));
        _other = Path.Combine(Path.GetTempPath(), "xplat-other-" + Guid.NewGuid().ToString("N"));
        TestAppBuilder.Preferences.AutoPopStashAfterPull = null;
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.AutoPopStashAfterPull = null;
        GitProcess.DeleteFolder(_remote);
        GitProcess.DeleteFolder(_other);
        _repo.Dispose();
    }

    [AvaloniaTest]
    public async Task A_pull_with_auto_stash_stashes_pulls_and_pops_after_upstreams_question()
    {
        string branch = PushTheirCommit();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "local edit");
        Window owner = new();
        owner.Show();
        RepositoryOperationsViewModel actions = new(new GitOperations());

        Task<bool> pull = PullAutoStashFlow.PullAsync(actions, TestAppBuilder.Preferences, _repo.Path,
            new PullRequest("origin", branch, PullAction.Merge, Prune: false, AutoStash: true), () => owner);

        WaitUntil(() => owner.OwnedWindows.OfType<MessageBoxWindow>().Any());
        _repo.ReadFile("theirs.txt").Should().Be("theirs");
        _repo.ReadFile("a.txt").Should().Be("two", "the local edit is stashed while the question shows");
        MessageBoxWindow question = owner.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be(CheckoutBranchDialog.ApplyStashedItemsAgainCaption);
        question.GetLogicalDescendants().OfType<CheckBox>().Single(check => check.Name == "VerificationCheck")
            .IsChecked = true;
        question.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.Content is AccessText { Text: "_Yes" })
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => pull.IsCompleted);
        (await pull).Should().BeTrue();
        _repo.ReadFile("a.txt").Should().Be("local edit");
        _repo.Run("stash", "list").Trim().Should().BeEmpty();
        TestAppBuilder.Preferences.AutoPopStashAfterPull.Should().BeTrue();
        owner.Close();
    }

    // The remote gets a commit from another clone, which the repository then pulls.
    private string PushTheirCommit()
    {
        Directory.CreateDirectory(_remote);
        GitProcess.Run(_remote, "init", "--bare", "-q");
        string branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("remote", "add", "origin", _remote);
        _repo.Run("push", "-q", "-u", "origin", branch);
        GitProcess.Run(Path.GetTempPath(), "clone", "-q", _remote, _other);
        File.WriteAllText(Path.Combine(_other, "theirs.txt"), "theirs");
        GitProcess.Run(_other, "add", "theirs.txt");
        GitProcess.Run(_other, "-c", "user.name=Other", "-c", "user.email=other@example.com", "commit", "-q", "-m",
            "theirs");
        GitProcess.Run(_other, "push", "-q", "origin", branch);
        return branch;
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
