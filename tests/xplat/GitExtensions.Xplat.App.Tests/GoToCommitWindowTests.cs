using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitExtensions.Extensibility.Git;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class GoToCommitWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _first = "";
    private string _second = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _first = _repo.Run("rev-parse", "HEAD~1").Trim();
        _second = _repo.Run("rev-parse", "HEAD").Trim();
        _repo.Run("tag", "v1", "HEAD~1");
        _repo.Run("branch", "older", "HEAD~1");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Go_resolves_the_commit_expression()
    {
        (Window owner, GoToCommitWindow window, Task<ObjectId?> result) = Open(clipboard: "");
        Find<TextBox>(window, "textboxCommitExpression").Text = "  HEAD~1 ";

        Click(window, "goButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().Be(ObjectId.Parse(_first));
        owner.Close();
    }

    [AvaloniaTest]
    public void The_focused_drop_down_supplies_the_tag_or_branch_and_an_unknown_one_resolves_to_nothing()
    {
        (Window owner, GoToCommitWindow window, Task<ObjectId?> result) = Open(clipboard: "");
        ComboBox tags = Find<ComboBox>(window, "comboBoxTags");
        tags.Items.Cast<string>().Should().Equal("v1");
        Find<ComboBox>(window, "comboBoxBranches").Items.Cast<string>().Should().Contain("older");
        Find<TextBox>(window, "textboxCommitExpression").Text = "HEAD";
        tags.Focus();
        tags.Text = "v2";

        Click(window, "goButton");

        WaitUntil(() => result.IsCompleted);
        result.Result.Should()
            .Be(default(ObjectId), "the focused tag box supplies the revision, and it has no such tag");
        owner.Close();
    }

    [AvaloniaTest]
    public void A_branch_picked_in_the_drop_down_goes_at_once()
    {
        (Window owner, GoToCommitWindow window, Task<ObjectId?> result) = Open(clipboard: "");
        ComboBox branches = Find<ComboBox>(window, "comboBoxBranches");

        branches.IsDropDownOpen = true;
        branches.SelectedItem = "older";
        branches.IsDropDownOpen = false;

        WaitUntil(() => result.IsCompleted);
        result.Result.Should().Be(ObjectId.Parse(_first));
        owner.Close();
    }

    [AvaloniaTest]
    public void A_commit_in_the_clipboard_fills_the_expression()
    {
        (Window owner, GoToCommitWindow window, _) = Open(clipboard: _second[..10]);

        Find<TextBox>(window, "textboxCommitExpression").Text.Should().Be(_second[..10]);
        window.Close();
        owner.Close();
    }

    [AvaloniaTest]
    public void Text_that_names_no_commit_stays_out_of_the_expression()
    {
        (Window owner, GoToCommitWindow window, _) = Open(clipboard: "not a revision");

        Find<TextBox>(window, "textboxCommitExpression").Text.Should().BeNullOrEmpty();
        window.Title.Should().Be("Go to commit");
        window.Close();
        owner.Close();
    }

    private (Window Owner, GoToCommitWindow Window, Task<ObjectId?> Result) Open(string clipboard)
    {
        Window owner = new();
        owner.Show();
        Task setClipboard = TopLevel.GetTopLevel(owner)!.Clipboard!.SetTextAsync(clipboard);
        WaitUntil(() => setClipboard.IsCompleted);
        GoToCommitWindow window = new(_repo.Path);
        Task<ObjectId?> result = window.ShowDialog<ObjectId?>(owner);
        WaitUntil(() => window.RefsLoaded.IsCompleted);
        return (owner, window, result);
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}");

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
