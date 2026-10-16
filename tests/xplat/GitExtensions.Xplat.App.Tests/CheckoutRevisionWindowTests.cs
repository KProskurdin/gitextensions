using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CheckoutRevisionWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _repo.Run("tag", "v1", "HEAD~1");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [Test]
    public void The_commit_pickers_texts_are_upstreams()
    {
        string picker = File.ReadAllText(FindUpstream("CommitPickerSmallControl.cs"));
        picker.Should().Contain($"\"{CommitPickerView.InvalidCommitHash}\"");
        File.ReadAllText(FindUpstream("TranslatedStrings.cs", "GitUI")).Should()
            .Contain($"_error = new(\"{CommitPickerView.ErrorCaption}\")");
    }

    [AvaloniaTest]
    public void Checkout_of_a_tag_from_the_left_panel_detaches_HEAD_at_the_tagged_commit()
    {
        string tagged = _repo.Run("rev-parse", "--short=8", "v1").Trim();
        MainWindow window = OpenWindow();
        TreeView tree = Find<TreeView>(window, "BranchTreeView");
        WaitUntil(() => TagNode(tree) is not null);
        tree.SelectedItem = TagNode(tree);

        Click(window, "TreeCheckoutMenuItem");

        CheckoutRevisionWindow checkout = WaitForWindow(window);
        checkout.FindControl<CommitPickerView>("CommitPicker")!.FindControl<TextBox>("CommitHashBox")!.Text.Should().Be(tagged);
        Click(checkout, "CheckoutButton");
        WaitUntil(() => _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim() == "HEAD");
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("first");
    }

    [AvaloniaTest]
    public void Force_resets_the_local_changes()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "local edit");
        MainWindow window = OpenWindow();
        Click(window, "CheckoutRevisionMenuItem");
        CheckoutRevisionWindow checkout = WaitForWindow(window);
        CommitPickerView picker = checkout.FindControl<CommitPickerView>("CommitPicker")!;

        RunUntilDone(picker.SetCommitAsync("v1"));
        checkout.FindControl<CheckBox>("ForceCheck")!.IsChecked = true;
        Click(checkout, "CheckoutButton");

        WaitUntil(() => _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim() == "HEAD");
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("one");
    }

    [AvaloniaTest]
    public void An_unknown_revision_is_refused_and_the_previous_commit_kept()
    {
        MainWindow window = OpenWindow();
        Click(window, "CheckoutRevisionMenuItem");
        CheckoutRevisionWindow checkout = WaitForWindow(window);
        CommitPickerView picker = checkout.FindControl<CommitPickerView>("CommitPicker")!;
        RunUntilDone(picker.SetCommitAsync("v1"));

        Task unknown = picker.SetCommitAsync("no-such-ref");

        WaitUntil(() => checkout.OwnedWindows.OfType<MessageBoxWindow>().Any());
        checkout.OwnedWindows.OfType<MessageBoxWindow>().Single().Close();
        RunUntilDone(unknown);
        picker.Selected!.Value.ToShortString().Should().Be(_repo.Run("rev-parse", "--short=8", "v1").Trim());
        checkout.Close();
    }

    private static string FindUpstream(string file, string? folder = null, [System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));
        return Directory.EnumerateFiles(Path.Combine(root, "src", "app", "GitUI"), file, SearchOption.AllDirectories)
            .First(path => folder is null || Path.GetFileName(Path.GetDirectoryName(path)) == folder);
    }

    private MainWindow OpenWindow()
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy && Roots(window.FindControl<TreeView>("BranchTreeView")!).Count > 0);
        return window;
    }

    private static CheckoutRevisionWindow WaitForWindow(Window owner)
    {
        WaitUntil(() => owner.OwnedWindows.OfType<CheckoutRevisionWindow>().SingleOrDefault()?.RevisionLoaded.IsCompleted == true);
        return owner.OwnedWindows.OfType<CheckoutRevisionWindow>().Single();
    }

    private static IReadOnlyList<RepoObjectNode> Roots(TreeView tree)
        => tree.ItemsSource as IReadOnlyList<RepoObjectNode> ?? [];

    private static RepoObjectNode? TagNode(TreeView tree)
        => Roots(tree).SelectMany(root => root.Descendants()).FirstOrDefault(node => node is { Kind: RepoObjectKind.Tag, Name: "v1" });

    private static void RunUntilDone(Task task) => WaitUntil(() => task.IsCompleted);

    private static void Click(Window window, string name)
    {
        Control control = Find<Control>(window, name);
        control.RaiseEvent(new RoutedEventArgs(control is MenuItem ? MenuItem.ClickEvent : Button.ClickEvent));
    }

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
