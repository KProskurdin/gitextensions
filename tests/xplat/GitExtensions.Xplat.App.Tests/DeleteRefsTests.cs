using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class DeleteRefsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _current = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _current = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();
        _repo.Run("branch", "merged", "HEAD~1");
        _repo.Run("branch", "other", "HEAD~1");
        _repo.Run("tag", "v1", "HEAD~1");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Delete_branch_drops_unknown_names_refuses_the_current_branch_and_deletes_merged_ones()
    {
        MainWindow window = Open();
        DeleteBranchWindow delete = OpenDeleteBranch(window);
        ComboBox branches = Find<ComboBox>(delete, "Branches");
        branches.Items.Cast<string>().Should().Contain(["merged", "other", _current]);

        branches.Text = _current;
        Click(delete, "Delete");
        MessageBoxWindow refused = WaitForMessage(delete);
        refused.Title.Should().Be(DeleteBranchDialog.DeleteBranchCaption);
        Find<SelectableTextBlock>(refused, "MessageText").Text.Should()
            .Be(string.Format(DeleteBranchDialog.CannotDeleteCurrentBranch, _current));
        refused.Close();

        branches.Text = "merged nosuch";
        Click(delete, "Delete");
        MessageBoxWindow unknown = WaitForMessage(delete);
        Find<SelectableTextBlock>(unknown, "MessageText").Text.Should()
            .Be(string.Format(DeleteBranchDialog.BranchNotSelectable, "nosuch"));
        unknown.Close();

        WaitUntil(() => !delete.IsVisible);
        _repo.Run("branch", "--list", "merged").Trim().Should().BeEmpty();
        _repo.Run("branch", "--list", "other").Trim().Should().NotBeEmpty();
    }

    [AvaloniaTest]
    public void The_button_checks_branches_in_upstreams_multiple_branches_window()
    {
        MainWindow window = Open();
        DeleteBranchWindow delete = OpenDeleteBranch(window);
        ComboBox branches = Find<ComboBox>(delete, "Branches");
        branches.Text = "merged";

        Click(delete, "selectMultipleBranchesButton");
        WaitUntil(() => delete.OwnedWindows.OfType<SelectMultipleBranchesWindow>().Any());
        SelectMultipleBranchesWindow select = delete.OwnedWindows.OfType<SelectMultipleBranchesWindow>().Single();
        List<CheckBox> checks = [.. Find<ListBox>(select, "Branches").Items.Cast<CheckBox>()];
        checks.Single(check => (string)check.Content! == "merged").IsChecked.Should().BeTrue();
        checks.Single(check => (string)check.Content! == "other").IsChecked = true;
        Click(select, "okButton");

        WaitUntil(() => branches.Text == "merged other");
        delete.Close();
    }

    [AvaloniaTest]
    public void A_branch_of_a_linked_worktree_goes_with_its_worktree_after_upstreams_question()
    {
        string worktree = _repo.Path.TrimEnd('/', '\\') + "-wt";
        _repo.Run("worktree", "add", "-q", worktree, "other");
        MainWindow window = Open();
        DeleteBranchWindow delete = OpenDeleteBranch(window);
        Find<ComboBox>(delete, "Branches").Text = "other";

        Click(delete, "Delete");
        MessageBoxWindow question = WaitForMessage(delete);
        question.GetLogicalDescendants().OfType<Button>().Single(button => button.IsDefault).Content
            .Should().BeOfType<AccessText>().Which.Text.Should().Be("_No", "upstream's default is No");
        question.GetLogicalDescendants().OfType<Button>()
            .Single(button => button.Content is AccessText { Text: "_Yes" })
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => !delete.IsVisible);
        Directory.Exists(worktree).Should().BeFalse();
        _repo.Run("branch", "--list", "other").Trim().Should().BeEmpty();
    }

    [AvaloniaTest]
    public void Delete_tag_deletes_the_named_tag_and_offers_the_remote_only_when_asked()
    {
        MainWindow window = Open();
        Click(window, "DeleteTagMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<DeleteTagWindow>().Any());
        DeleteTagWindow delete = window.OwnedWindows.OfType<DeleteTagWindow>().Single();
        Find<ComboBox>(delete, "Tags").Items.Cast<string>().Should().Equal("v1");
        ComboBox remotes = Find<ComboBox>(delete, "Remotes");
        remotes.IsEnabled.Should().BeFalse();
        Find<CheckBox>(delete, "deleteTag").IsChecked = true;
        remotes.IsEnabled.Should().BeTrue();
        Find<CheckBox>(delete, "deleteTag").IsChecked = false;
        Find<ComboBox>(delete, "Tags").Text = "v1";

        Click(delete, "Ok");

        WaitUntil(() => !delete.IsVisible);
        WaitUntil(() => _repo.Run("tag", "--list").Trim().Length == 0);
    }

    private MainWindow Open()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static DeleteBranchWindow OpenDeleteBranch(MainWindow window)
    {
        Click(window, "DeleteBranchMenuItem");
        WaitUntil(() => window.OwnedWindows.OfType<DeleteBranchWindow>().Any());
        return window.OwnedWindows.OfType<DeleteBranchWindow>().Single();
    }

    private static MessageBoxWindow WaitForMessage(Window owner)
    {
        WaitUntil(() => owner.OwnedWindows.OfType<MessageBoxWindow>().Any());
        return owner.OwnedWindows.OfType<MessageBoxWindow>().Single();
    }

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));
    }

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
