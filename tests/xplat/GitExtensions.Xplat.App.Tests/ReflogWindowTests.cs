using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ReflogWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _branch = "";
    private readonly List<ObjectId> _branchesCreated = [];
    private readonly List<(ObjectId Commit, ResetMode Mode)> _resets = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _branchesCreated.Clear();
        _resets.Clear();
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void The_window_lists_heads_reflog_and_the_references()
    {
        _repo.Run("branch", "other");

        ReflogWindow reflog = Open();

        ListBox entries = Find<ListBox>(reflog, "EntryList");
        entries.ItemsSource.Should().BeAssignableTo<IReadOnlyList<ReflogEntry>>().Which.Should().HaveCount(2);
        ((IReadOnlyList<ReflogEntry>)entries.ItemsSource!)[0].Ref.Should().Be("HEAD@{0}");
        (Find<ComboBox>(reflog, "ReferencesBox").ItemsSource as IEnumerable<string>).Should()
            .Equal(["HEAD", .. new[] { "other", _branch }.Order(StringComparer.Ordinal)]);
        Find<HyperlinkButton>(reflog, "CurrentBranchLink").Content.Should().Be($"current branch ({_branch})");
        Find<TextBlock>(reflog, "DirtyWarningText").IsVisible.Should().BeFalse();

        Click(reflog, "CurrentBranchLink");

        WaitUntil(() => reflog.EntriesLoaded.IsCompleted
                        && ((IReadOnlyList<ReflogEntry>?)entries.ItemsSource)?.FirstOrDefault()?.Ref == $"{_branch}@{{0}}");
        reflog.Close();
    }

    [AvaloniaTest]
    public void The_menu_copies_the_hash_and_opens_the_branch_and_reset_windows()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "dirty");
        ReflogWindow reflog = Open();
        Find<TextBlock>(reflog, "DirtyWarningText").IsVisible.Should().BeTrue();
        ListBox entries = Find<ListBox>(reflog, "EntryList");
        entries.SelectedIndex = 1;
        ReflogEntry entry = (ReflogEntry)entries.SelectedItem!;

        Click(reflog, "CopyShaMenuItem");
        Task<string?> text = TopLevel.GetTopLevel(reflog)!.Clipboard!.TryGetTextAsync();
        WaitUntil(() => text.IsCompleted);
        text.Result.Should().Be(entry.Sha.ToString());

        Click(reflog, "CreateBranchMenuItem");
        WaitUntil(() => _branchesCreated.Count == 1);
        _branchesCreated.Should().Equal(entry.Sha);

        Click(reflog, "ResetBranchMenuItem");
        WaitUntil(() => reflog.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow question = reflog.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be(ReflogWindow.ContinueResetCaption);
        Click(question, "YesButton");
        WaitUntil(() => _resets.Count == 1);
        _resets.Should().Equal((entry.Sha, ResetMode.Soft));
        reflog.Close();
    }

    [AvaloniaTest]
    public void The_reset_window_resets_the_branch_as_chosen()
    {
        string first = _repo.Run("rev-parse", "HEAD~1").Trim();
        Window owner = new();
        owner.Show();
        ResetCurrentBranchWindow reset = new(_repo.Path, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, "HEAD~1", ResetMode.Mixed, _ => Task.FromResult(true));
        Task<bool> result = reset.ShowDialog<bool>(owner);
        WaitUntil(() => reset.ResetLoaded.IsCompleted);

        Find<TextBlock>(reset, "BranchInfoText").Text.Should().Be($"Reset branch '{_branch}' to revision:");
        Find<TextBlock>(reset, "MessageText").Text.Should().Be("first");
        Find<RadioButton>(reset, "MixedRadio").IsChecked.Should().BeTrue();
        Click(reset, "OkButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().BeTrue();
        _repo.Run("rev-parse", "HEAD").Trim().Should().Be(first);
        _repo.Run("status", "--porcelain").Trim().Should().Be("M a.txt", "a mixed reset keeps the working tree");
        owner.Close();
    }

    private ReflogWindow Open()
    {
        Window owner = new();
        owner.Show();
        ReflogWindow reflog = new(_repo.Path,
            (_, id) =>
            {
                _branchesCreated.Add(id);
                return Task.CompletedTask;
            },
            (_, id, mode) =>
            {
                _resets.Add((id, mode));
                return Task.CompletedTask;
            });
        _ = reflog.ShowDialog(owner);
        WaitUntil(() => reflog.ReflogLoaded.IsCompleted);
        return reflog;
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));
    }

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
