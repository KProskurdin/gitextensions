using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class CloneWindowTests
{
    private const string Url = "https://example.com/owner/project.git";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _parent = "";
    private readonly List<string> _added = [];
    private readonly List<string> _opened = [];

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _parent = Path.Combine(Path.GetTempPath(), "xplat-clones-" + Guid.NewGuid().ToString("N"));
        _added.Clear();
        _opened.Clear();
        TestAppBuilder.RemoteUrls.Entries.Clear();
        TestAppBuilder.Preferences.DefaultCloneDestinationPath = "";
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.RemoteUrls.Entries.Clear();
        TestAppBuilder.Preferences.DefaultCloneDestinationPath = "";
        GitProcess.DeleteFolder(_parent);
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_starts_with_upstreams_defaults()
    {
        TestAppBuilder.Preferences.DefaultCloneDestinationPath = _parent;

        CloneWindow clone = Open(repositoryPath: null, clipboard: "no url here");

        Find<ComboBox>(clone, "FromBox").Text.Should().BeEmpty();
        Find<ComboBox>(clone, "ToBox").Text.Should().Be(_parent);
        Find<ComboBox>(clone, "BranchesBox").Text.Should().Be(CloneDialog.BranchDefaultRemoteHead);
        Find<RadioButton>(clone, "PersonalRepositoryRadio").IsChecked.Should().BeTrue();
        Find<CheckBox>(clone, "InitializeSubmodulesCheck").IsChecked.Should().BeTrue();
        Find<CheckBox>(clone, "DownloadFullHistoryCheck").IsChecked.Should().BeTrue();
        TextBlock info = Find<TextBlock>(clone, "InfoText");
        info.Text.Should().Be(string.Format(CloneDialog.InfoNewRepositoryLocation,
            Path.Combine(_parent, "[Subdirectory to create:]")));
        info.Classes.Should().Contain("error");

        Find<ComboBox>(clone, "FromBox").Text = Url;

        Find<TextBox>(clone, "SubdirectoryBox").Text.Should().Be("project");
        info.Text.Should().EndWith(CloneDialog.InfoDirectoryNew);
        info.Classes.Should().NotContain("error");
        clone.Close();
    }

    [AvaloniaTest]
    public void A_url_in_the_clipboard_is_proposed()
    {
        CloneWindow clone = Open(repositoryPath: null, clipboard: $"clone {Url}");

        Find<ComboBox>(clone, "FromBox").Text.Should().Be(Url);
        Find<TextBox>(clone, "SubdirectoryBox").Text.Should().Be("project");
        clone.Close();
    }

    [AvaloniaTest]
    public void The_open_repositorys_remote_and_parent_folder_are_proposed()
    {
        _repo.Run("remote", "add", "origin", Url);

        CloneWindow clone = Open(_repo.Path, clipboard: "");

        Find<ComboBox>(clone, "FromBox").Text.Should().Be(Url);
        Find<ComboBox>(clone, "ToBox").Text.Should()
            .Be(Path.GetDirectoryName(_repo.Path.TrimEnd(Path.DirectorySeparatorChar)));
        clone.Close();
    }

    [AvaloniaTest]
    public void The_branch_list_reads_the_remotes_branches_when_it_opens()
    {
        _repo.Run("branch", "topic");
        CloneWindow clone = Open(repositoryPath: null, clipboard: "");
        Find<ComboBox>(clone, "FromBox").Text = _repo.Path;
        ComboBox branches = Find<ComboBox>(clone, "BranchesBox");

        branches.IsDropDownOpen = true;

        WaitUntil(() => clone.BranchesLoaded.IsCompleted
                        && (branches.ItemsSource as IEnumerable<string>)?.Contains("topic") == true);
        (branches.ItemsSource as IEnumerable<string>).Should().StartWith(CloneDialog.DefaultBranchItems);
        branches.Text.Should().Be(CloneDialog.BranchDefaultRemoteHead);
        branches.IsDropDownOpen = false;
        clone.Close();
    }

    [AvaloniaTest]
    public void A_clone_without_a_destination_says_so_and_stays_open()
    {
        CloneWindow clone = Open(repositoryPath: null, clipboard: "");
        Find<ComboBox>(clone, "FromBox").Text = _repo.Path;

        Click(clone, "CloneButton");

        WaitUntil(() => clone.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow error = clone.OwnedWindows.OfType<MessageBoxWindow>().Single();
        error.Title.Should().Be(CloneDialog.ErrorCloneFailed);
        Find<SelectableTextBlock>(error, "MessageText").Text.Should().Be(CloneDialog.ErrorDestinationNotSupplied);
        Click(error, "OKButton");
        WaitUntil(() => !error.IsVisible);
        clone.IsVisible.Should().BeTrue();
        clone.Close();
    }

    [AvaloniaTest]
    public void A_clone_is_made_remembered_and_opened_when_asked()
    {
        // A file URL, as git ignores the depth of a clone from a local path.
        string source = new Uri(_repo.Path).AbsoluteUri;
        CloneWindow clone = Open(repositoryPath: null, clipboard: "");
        Find<ComboBox>(clone, "FromBox").Text = source;
        Find<ComboBox>(clone, "ToBox").Text = _parent;
        Find<TextBox>(clone, "SubdirectoryBox").Text = "copy";
        Find<CheckBox>(clone, "DownloadFullHistoryCheck").IsChecked = false;

        Click(clone, "CloneButton");

        WaitUntil(() => clone.OwnedWindows.OfType<MessageBoxWindow>().Any());
        string target = Path.Combine(_parent, "copy");
        MessageBoxWindow question = clone.OwnedWindows.OfType<MessageBoxWindow>().Single();
        question.Title.Should().Be(CloneDialog.QuestionOpenRepoCaption);
        Find<SelectableTextBlock>(question, "MessageText").Text.Should()
            .Be(string.Format(CloneDialog.QuestionOpenRepo, target));
        Click(question, "YesButton");
        WaitUntil(() => !clone.IsVisible);

        File.Exists(Path.Combine(target, "a.txt")).Should().BeTrue();
        File.Exists(Path.Combine(target, ".git", "shallow")).Should().BeTrue();
        TestAppBuilder.RemoteUrls.Entries.Select(entry => entry.Path).Should().Equal(source);
        _added.Should().Equal(target);
        _opened.Should().Equal(target);
    }

    [AvaloniaTest]
    public void A_bare_clone_is_not_opened_when_declined()
    {
        CloneWindow clone = Open(repositoryPath: null, clipboard: "");
        Find<ComboBox>(clone, "FromBox").Text = _repo.Path;
        Find<ComboBox>(clone, "ToBox").Text = _parent;
        Find<TextBox>(clone, "SubdirectoryBox").Text = "bare.git";
        Find<RadioButton>(clone, "CentralRepositoryRadio").IsChecked = true;

        Click(clone, "CloneButton");

        WaitUntil(() => clone.OwnedWindows.OfType<MessageBoxWindow>().Any());
        Click(clone.OwnedWindows.OfType<MessageBoxWindow>().Single(), "NoButton");
        WaitUntil(() => !clone.IsVisible);
        string target = Path.Combine(_parent, "bare.git");
        File.Exists(Path.Combine(target, "HEAD")).Should().BeTrue();
        Directory.Exists(Path.Combine(target, ".git")).Should().BeFalse();
        _opened.Should().BeEmpty();
    }

    [AvaloniaTest]
    public void The_clone_menu_opens_the_clone_window_on_the_default_destination()
    {
        TestAppBuilder.Preferences.DefaultCloneDestinationPath = _parent;
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();

        Find<MenuItem>(window, "CloneMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        WaitUntil(() => window.OwnedWindows.OfType<CloneWindow>().Any());
        CloneWindow clone = window.OwnedWindows.OfType<CloneWindow>().Single();
        WaitUntil(() => clone.CloneLoaded.IsCompleted);
        Find<ComboBox>(clone, "ToBox").Text.Should().Be(_parent);
        clone.Close();
        window.Close();
    }

    private CloneWindow Open(string? repositoryPath, string clipboard)
    {
        Window owner = new();
        owner.Show();
        Task setClipboard = TopLevel.GetTopLevel(owner)!.Clipboard!.SetTextAsync(clipboard);
        WaitUntil(() => setClipboard.IsCompleted);
        CloneWindow clone = new(repositoryPath, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, [],
            path =>
            {
                _added.Add(path);
                return Task.CompletedTask;
            },
            path =>
            {
                _opened.Add(path);
                return Task.CompletedTask;
            });
        _ = clone.ShowDialog<bool>(owner);
        WaitUntil(() => clone.CloneLoaded.IsCompleted);
        return clone;
    }

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
