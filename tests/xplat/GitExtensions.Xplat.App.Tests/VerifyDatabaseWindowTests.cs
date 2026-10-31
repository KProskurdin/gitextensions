using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class VerifyDatabaseWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _head = "";
    private string _lostCommit = "";
    private string _lostBlob = "";

    // A commit, an annotated tag (on HEAD) and a blob that nothing references any more (only the reflog keeps the
    // commit).
    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _head = _repo.Run("rev-parse", "HEAD").Trim();
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "three");
        _repo.Run("commit", "-q", "-am", "lost commit");
        _lostCommit = _repo.Run("rev-parse", "HEAD").Trim();
        _repo.Run("tag", "-a", "lost-tag", "-m", "tag message", _head);
        _repo.Run("reset", "-q", "--hard", "HEAD~1");
        _repo.Run("tag", "-d", "lost-tag");
        File.WriteAllText(Path.Combine(_repo.Path, "blob.json"), "{\"lost\": true}");
        _lostBlob = _repo.Run("hash-object", "-w", "blob.json").Trim();
        File.Delete(Path.Combine(_repo.Path, "blob.json"));
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Load_should_read_upstreams_data_of_lost_commits_tags_and_blobs()
    {
        IReadOnlyList<LostObject> lostObjects = [];
        Task load = Task.Run(() => lostObjects =
            VerifyDatabase.Load(GitModules.Open(_repo.Path), VerifyDatabase.Options(false, false, noReflogs: true)));
        WaitUntil(() => load.IsCompleted);
        load.Exception.Should().BeNull();

        LostObject commit = lostObjects.Single(o => o.ObjectType == LostObjectType.Commit);
        commit.ObjectId.ToString().Should().Be(_lostCommit);
        commit.RawType.Should().Be("dangling commit");
        commit.Subject.Should().Be("lost commit");
        commit.Author.Should().Be("Test");
        commit.Parent.ToString().Should().Be(_head);
        commit.Date.Should().NotBeNull();

        LostObject tag = lostObjects.Single(o => o.ObjectType == LostObjectType.Tag);
        tag.TagName.Should().Be("lost-tag");
        tag.Subject.Should().Be("lost-tag:tag message");
        tag.Parent.ToString().Should().Be(_head);

        lostObjects.Should().Contain(o => o.ObjectType == LostObjectType.Blob && o.ObjectId.ToString() == _lostBlob);
    }

    [AvaloniaTest]
    public void The_window_lists_commits_and_tags_and_shows_blobs_with_their_guessed_type()
    {
        (Window owner, VerifyDatabaseWindow window) = Open();

        window.Rows.Select(row => row.Object.ObjectType).Should().BeEquivalentTo(
            [LostObjectType.Commit, LostObjectType.Tag], "commits and annotated tags are shown at first");
        Find<Button>(window, "columnAuthor").IsVisible.Should().BeTrue();

        Find<CheckBox>(window, "ShowOtherObjects").IsChecked = true;
        Find<CheckBox>(window, "ShowCommitsAndTags").IsChecked = false;
        WaitUntil(() => window.Rows.Any(row => row.Object.RawType.Contains("seemingly")));
        LostObjectRow blob = window.Rows.Single(row => row.Object.ObjectId.ToString() == _lostBlob);
        blob.Object.RawType.Should().Be("dangling blob (seemingly: json)");
        window.Rows.Should().OnlyContain(row => !row.Object.IsCommitOrTag);
        Find<Button>(window, "columnAuthor").IsVisible.Should().BeFalse("the commit columns go with the commits");

        Find<CheckBox>(window, "ShowOtherObjects").IsChecked = false;
        Find<CheckBox>(window, "ShowCommitsAndTags").IsChecked.Should().BeTrue("one of the two stays checked");
        owner.Close();
    }

    [AvaloniaTest]
    public void Selecting_an_object_previews_it_and_the_menu_follows_its_type()
    {
        (Window owner, VerifyDatabaseWindow window) = Open();
        ListBox list = Find<ListBox>(window, "Warnings");

        list.SelectedItem = window.Rows.Single(row => row.Object.ObjectType == LostObjectType.Commit);
        WaitUntil(() => window.PreviewShown.IsCompleted);
        Find<DiffView>(window, "fileViewer").FindControl<TextBlock>("TitleText")!.Text.Should().Be("commit.patch");
        OpenMenu(list);
        Find<MenuItem>(window, "mnuLostObjectsCreateBranch").IsEnabled.Should().BeTrue();
        Find<MenuItem>(window, "saveAsToolStripMenuItem").IsEnabled.Should().BeFalse();

        list.SelectedItem = window.Rows.Single(row => row.Object.ObjectType == LostObjectType.Tag);
        OpenMenu(list);
        Find<MenuItem>(window, "mnuLostObjectsCreateBranch").IsEnabled.Should()
            .BeFalse("upstream creates branches and tags on commits only");
        owner.Close();
    }

    [AvaloniaTest]
    public void Recover_tags_the_checked_objects_and_Delete_all_removes_the_tags()
    {
        (Window owner, VerifyDatabaseWindow window) = Open();

        Click(window, "btnRestoreSelectedObjects");
        CloseMessageBox(window, "OKButton");
        _repo.Run("tag", "--list").Should().BeEmpty("nothing was checked");

        window.Rows.Single(row => row.Object.ObjectType == LostObjectType.Commit).IsSelected = true;
        Click(window, "btnRestoreSelectedObjects");
        CloseMessageBox(window, "OKButton");
        WaitUntil(() => window.ObjectsLoaded.IsCompleted && window.Rows.Count == 1);
        _repo.Run("rev-parse", "LOST_FOUND_1").Trim().Should().Be(_lostCommit);
        window.Rows.Single().Object.ObjectType.Should().Be(LostObjectType.Tag, "the tagged commit is no longer lost");

        Click(window, "DeleteAllLostAndFoundTags");
        WaitUntil(() => window.ObjectsLoaded.IsCompleted && window.Rows.Count == 2);
        _repo.Run("tag", "--list").Should().BeEmpty();
        owner.Close();
    }

    [AvaloniaTest]
    public void Recovering_every_listed_object_closes_the_window()
    {
        (Window owner, VerifyDatabaseWindow window) = Open();

        Find<CheckBox>(window, "columnIsLostObjectSelected").IsChecked = true;
        window.Rows.Should().OnlyContain(row => row.IsSelected);
        Click(window, "btnRestoreSelectedObjects");
        CloseMessageBox(window, "OKButton");

        WaitUntil(() => !window.IsVisible);
        _repo.Run("tag", "--list").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Should().HaveCount(2).And.Contain("LOST_FOUND_lost-tag", "an annotated tag keeps its name");
        owner.Close();
    }

    [AvaloniaTest]
    public void Remove_and_Save_run_upstreams_commands_after_the_question()
    {
        List<string> commands = [];
        (Window owner, VerifyDatabaseWindow window) = Open(arguments =>
        {
            commands.Add(arguments);
            return Task.FromResult(true);
        });

        Click(window, "Remove");
        CloseMessageBox(window, "NoButton");
        commands.Should().BeEmpty();
        Click(window, "Remove");
        CloseMessageBox(window, "YesButton");
        WaitUntil(() => commands.Count == 1);
        Click(window, "SaveObjects");
        WaitUntil(() => commands.Count == 2);

        commands.Should().Equal("prune", "fsck-objects --lost-found --no-reflogs");
        owner.Close();
    }

    [AvaloniaTest]
    public void The_git_maintenance_menu_deletes_index_lock_and_edits_git_config_with_a_warning()
    {
        string indexLock = Path.Combine(_repo.Path, ".git", "index.lock");
        File.WriteAllText(indexLock, "");
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();
        main.OpenRepository(_repo.Path);
        WaitUntil(() => !main.IsBusy);
        main.FindControl<MenuItem>("GitMaintenanceMenuItem")!.IsEnabled.Should().BeTrue();

        main.FindControl<MenuItem>("DeleteIndexLockMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => !File.Exists(indexLock));

        main.FindControl<MenuItem>("EditLocalGitConfigMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<EditorWindow>().Any());
        EditorWindow editor = main.OwnedWindows.OfType<EditorWindow>().Single();
        editor.Title.Should().Be(Path.GetFullPath(Path.Combine(_repo.Path, ".git", "config")));
        Find<Border>(editor, "panelMessage").IsVisible.Should().BeTrue();
        editor.Close();

        main.FindControl<MenuItem>("RecoverLostObjectsMenuItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        WaitUntil(() => main.OwnedWindows.OfType<VerifyDatabaseWindow>().Any());
        main.OwnedWindows.OfType<VerifyDatabaseWindow>().Single().Close();
        main.Close();
    }

    private (Window Owner, VerifyDatabaseWindow Window) Open(Func<string, Task<bool>>? runGit = null)
    {
        Window owner = new();
        owner.Show();
        VerifyDatabaseWindow window = new(_repo.Path, runGit ?? (_ => Task.FromResult(true)),
            (_, _) => Task.FromResult(false), (_, _) => Task.FromResult(false));
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.Rows.Count > 0 && window.ObjectsLoaded.IsCompleted);
        return (owner, window);
    }

    private static void CloseMessageBox(Window window, string button)
    {
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow box = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Click(box, button);
        WaitUntil(() => !box.IsVisible);
    }

    private static void Click(Window window, string name)
        => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static T Find<T>(Window window, string name)
        where T : Control
        => window.FindControl<T>(name)
           ?? window.GetLogicalDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
           ?? throw new InvalidOperationException($"No control named {name}");

    // The menu enables its items for the selected object when it opens, as upstream's mnuLostObjects_Opening.
    private static void OpenMenu(ListBox list)
    {
        list.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        list.ContextMenu!.Close();
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
        }
    }
}
