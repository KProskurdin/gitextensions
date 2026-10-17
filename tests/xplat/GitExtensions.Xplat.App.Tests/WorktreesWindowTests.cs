using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class WorktreesWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly List<string> _opened = [];
    private TestRepository _repo = null!;
    private string _branch = "";

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _branch = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _opened.Clear();
        TestAppBuilder.Preferences.SetAsks(Confirmation.SwitchWorktree, true);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string worktree in Directory.GetDirectories(Path.GetDirectoryName(_repo.Path)!,
                     Path.GetFileName(_repo.Path) + "_*"))
        {
            GitProcess.DeleteFolder(worktree);
        }

        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_creates_opens_and_deletes_worktrees_but_never_the_main_one()
    {
        WorktreesWindow worktrees = Open();
        ListBox list = Find<ListBox>(worktrees, "WorktreeList");
        worktrees.Worktrees.Should().ContainSingle().Which.Branch.Should().Be(_branch);
        list.SelectedIndex = 0;
        Find<Button>(worktrees, "OpenButton").IsEnabled.Should().BeFalse("it is the open worktree");
        Find<Button>(worktrees, "DeleteButton").IsEnabled.Should().BeFalse();
        Find<Button>(worktrees, "PruneButton").IsEnabled.Should().BeFalse();

        Click(worktrees, "CreateButton");
        CreateWorktreeWindow create = WaitForOwned<CreateWorktreeWindow>(worktrees);
        WaitUntil(() => create.BranchesLoaded.IsCompleted);
        Find<RadioButton>(create, "ExistingBranchRadio").IsEnabled.Should().BeFalse("no other branch exists");
        Find<RadioButton>(create, "NewBranchRadio").IsChecked.Should().BeTrue();
        Find<TextBox>(create, "NewBranchNameBox").Text = "wt branch";
        Click(create, "CreateButton");

        // Upstream's WorktreeCreate offers to open the new worktree; declined here.
        ConfirmWindow question = WaitForOwned<ConfirmWindow>(worktrees);
        Click(question, "CancelButton");
        WaitUntil(() => worktrees.Worktrees.Count == 2);
        string folder = _repo.Path + "_wt_branch";
        worktrees.Worktrees[1].Path.Should().Be(Path.GetFullPath(folder));
        worktrees.Worktrees[1].Branch.Should().Be("wt_branch", "the name is normalised as upstream's");
        worktrees.ShouldRefresh.Should().BeTrue();
        _opened.Should().BeEmpty();

        list.SelectedIndex = 1;
        Find<Button>(worktrees, "DeleteButton").IsEnabled.Should().BeTrue();
        Click(worktrees, "DeleteButton");
        MessageBoxWindow delete = WaitForOwned<MessageBoxWindow>(worktrees);
        delete.Title.Should().Be(ManageWorktreeDialog.DeleteCaption);
        Find<TextBlock>(delete, "HeadingText").Text.Should().Be(ManageWorktreeDialog.CannotBeUndone);
        PressTaskDialogButton(delete, "_Yes");

        WaitUntil(() => worktrees.Worktrees.Count == 1);
        Directory.Exists(folder).Should().BeFalse();
        worktrees.Close();
    }

    [AvaloniaTest]
    public void Open_selected_asks_then_opens_the_worktree_and_closes()
    {
        string folder = _repo.Path + "_other";
        _repo.Run("worktree", "add", folder, "-b", "other");
        WorktreesWindow worktrees = Open();
        Find<ListBox>(worktrees, "WorktreeList").SelectedIndex = 1;

        Click(worktrees, "OpenButton");
        ConfirmWindow question = WaitForOwned<ConfirmWindow>(worktrees);
        question.Title.Should().Be(ManageWorktreeDialog.SwitchCaption);
        Find<TextBlock>(question, "MessageText").Text.Should()
            .Be(string.Format(Confirmations.SwitchWorktreeQuestion, Path.GetFullPath(folder)));
        Click(question, "ConfirmButton");

        WaitUntil(() => !worktrees.IsVisible);
        _opened.Should().Equal(Path.GetFullPath(folder));
    }

    [AvaloniaTest]
    public void A_worktree_whose_folder_is_gone_is_struck_out_and_can_be_pruned()
    {
        string folder = _repo.Path + "_gone";
        _repo.Run("worktree", "add", folder, "-b", "gone");
        GitProcess.DeleteFolder(folder);
        WorktreesWindow worktrees = Open();
        Find<ListBox>(worktrees, "WorktreeList").SelectedIndex = 1;

        worktrees.Worktrees[1].IsDeleted.Should().BeTrue();
        Find<Button>(worktrees, "OpenButton").IsEnabled.Should().BeFalse();
        Find<Button>(worktrees, "DeleteButton").IsEnabled.Should().BeFalse();
        Find<Button>(worktrees, "PruneButton").IsEnabled.Should().BeTrue();
        worktrees.GetLogicalDescendants().OfType<Grid>().Count(grid => grid.Classes.Contains("deleted")).Should().Be(1);

        Click(worktrees, "PruneButton");

        WaitUntil(() => worktrees.Worktrees.Count == 1);
        Find<Button>(worktrees, "PruneButton").IsEnabled.Should().BeFalse();
        worktrees.Close();
    }

    [AvaloniaTest]
    public void Create_checks_out_an_existing_branch_in_the_proposed_folder()
    {
        _repo.Run("branch", "feature/x");
        Window owner = new();
        owner.Show();
        CreateWorktreeWindow create = new(_repo.Path, _repo.Path, Operations(), TestAppBuilder.Preferences);
        Task<string?> result = create.ShowDialog<string?>(owner);
        WaitUntil(() => create.BranchesLoaded.IsCompleted);

        Find<ComboBox>(create, "BranchesBox").ItemsSource.Should().BeEquivalentTo(new[] { "feature/x" });
        Find<TextBox>(create, "DirectoryBox").Text.Should().Be(_repo.Path + "_feature_x");
        Find<Button>(create, "CreateButton").IsEnabled.Should().BeTrue();
        Click(create, "CreateButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().Be(_repo.Path + "_feature_x");
        _repo.Run("-C", result.Result!, "rev-parse", "--abbrev-ref", "HEAD").Trim().Should().Be("feature/x");
        owner.Close();
    }

    private static RepositoryOperationsViewModel Operations() => new(new GitOperations());

    private WorktreesWindow Open()
    {
        Window owner = new();
        owner.Show();
        WorktreeFlow flow = new(Operations(), TestAppBuilder.Preferences, path =>
        {
            _opened.Add(path);
            return Task.CompletedTask;
        });
        WorktreesWindow worktrees = new(_repo.Path, new GitRepositoryService(), flow);
        _ = worktrees.ShowDialog(owner);
        WaitUntil(() => worktrees.WorktreesLoaded.IsCompleted);
        return worktrees;
    }

    private static void PressTaskDialogButton(Window box, string text)
        => box.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.Content is AccessText { Text: var label } && label == text)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T WaitForOwned<T>(Window owner) where T : Window
    {
        WaitUntil(() => owner.OwnedWindows.OfType<T>().Any());
        return owner.OwnedWindows.OfType<T>().Single();
    }

    private static void Click(Window window, string name)
        => Find<Control>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
