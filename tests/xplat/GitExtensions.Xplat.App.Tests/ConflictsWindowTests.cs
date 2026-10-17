using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class ConflictsWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _base = "";
    private int _commitsOffered;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _base = _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim();

        // A stand-in merge tool, so the window does not open on upstream's "no mergetool" message.
        _repo.Run("config", "merge.tool", "fake");
        _repo.Run("config", "merge.guitool", "fake");
        _repo.Run("config", "mergetool.fake.path", GitPath());
        _repo.Run("config", "mergetool.fake.cmd", "version");
        _commitsOffered = 0;
        TestAppBuilder.Preferences.ResetConfirmations();
    }

    [TearDown]
    public void TearDown()
    {
        TestAppBuilder.Preferences.ResetConfirmations();
        _repo.Dispose();
    }

    [AvaloniaTest]
    public void The_window_describes_a_file_changed_on_both_sides()
    {
        MergeConflict(theirs: "theirs");

        ConflictsWindow conflicts = Open();

        conflicts.Conflicts.Select(conflict => conflict.Filename).Should().Equal("a.txt");
        Find<TextBlock>(conflicts, "ConflictDescriptionText").Text.Should()
            .Be("The file has been changed both locally (ours) and remotely (theirs). Merge the changes.");
        (Find<SelectableTextBlock>(conflicts, "LocalNameText").Text, Find<SelectableTextBlock>(conflicts, "BaseNameText").Text,
            Find<SelectableTextBlock>(conflicts, "RemoteNameText").Text).Should().Be(("a.txt", "a.txt", "a.txt"));
        Find<TextBlock>(conflicts, "LocalLabelText").Text.Should().Be("Local/current\u00A0(ours)");
        Find<TextBlock>(conflicts, "RemoteLabelText").Text.Should().Be("Remote/incoming\u00A0(theirs)");
        Find<Button>(conflicts, "OpenMergeToolButton").Content.Should().Be("Open in fake");
        Find<MenuItem>(conflicts, "ChooseLocalMenuItem").Header.Should().Be("Choose local/current (ours)");
        conflicts.Close();
    }

    [AvaloniaTest]
    public void Choosing_a_side_resolves_the_file_and_offers_to_commit()
    {
        MergeConflict(theirs: "theirs");
        ConflictsWindow conflicts = Open();

        Click(conflicts, "ChooseRemoteMenuItem");

        WaitUntil(() => conflicts.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow question = conflicts.OwnedWindows.OfType<ConfirmWindow>().Single();
        question.Title.Should().Be(ConflictsDialog.AllConflictsResolvedCaption);
        Click(question, "ConfirmButton");
        WaitUntil(() => !conflicts.IsVisible);
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("theirs");
        _commitsOffered.Should().Be(1);
    }

    [AvaloniaTest]
    public void A_file_deleted_remotely_is_deleted_when_chosen()
    {
        MergeConflict(theirs: null);
        ConflictsWindow conflicts = Open();
        Find<TextBlock>(conflicts, "ConflictDescriptionText").Text.Should()
            .Be("The file has been modified locally (ours) and deleted remotely (theirs). Choose to delete the file or keep the modified version.");
        Find<SelectableTextBlock>(conflicts, "RemoteNameText").Text.Should().Be("deleted");

        Click(conflicts, "MergeButton");

        WaitUntil(() => conflicts.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow solve = conflicts.OwnedWindows.OfType<MessageBoxWindow>().Single();
        solve.Title.Should().Be(ConflictsDialog.SolveMergeConflictCaption);
        Find<SelectableTextBlock>(solve, "MessageText").Text.Should().StartWith("File 'a.txt' does not have a remote revision.");
        solve.GetLogicalDescendants().OfType<Button>()
            .Single(button => (button.Content as Control)?.GetLogicalDescendants().OfType<AccessText>()
                .Any(text => text.Text == "Delete file (theirs)") == true)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        WaitUntil(() => conflicts.OwnedWindows.OfType<ConfirmWindow>().Any());
        Click(conflicts.OwnedWindows.OfType<ConfirmWindow>().Single(), "CancelButton");
        WaitUntil(() => !conflicts.IsVisible);
        File.Exists(Path.Combine(_repo.Path, "a.txt")).Should().BeFalse();
        _commitsOffered.Should().Be(0);
    }

    [AvaloniaTest]
    public void During_a_rebase_ours_and_theirs_swap()
    {
        MergeConflict(theirs: "theirs", rebase: true);

        ConflictsWindow conflicts = Open();

        Find<TextBlock>(conflicts, "LocalLabelText").Text.Should().Be("Local/current\u00A0(theirs)");
        Find<MenuItem>(conflicts, "ChooseLocalMenuItem").Header.Should().Be("Choose local/current (theirs)");
        Find<MenuItem>(conflicts, "ChooseRemoteMenuItem").Header.Should().Be("Choose remote/incoming (ours)");
        conflicts.Close();
    }

    [AvaloniaTest]
    public void Reset_asks_twice_and_resets_hard()
    {
        MergeConflict(theirs: "theirs");
        ConflictsWindow conflicts = Open();

        Click(conflicts, "ResetButton");
        WaitUntil(() => conflicts.OwnedWindows.OfType<MessageBoxWindow>().Any());
        Click(conflicts.OwnedWindows.OfType<MessageBoxWindow>().Single(), "YesButton");
        WaitUntil(() => conflicts.OwnedWindows.OfType<ConfirmWindow>().Any());
        ConfirmWindow warning = conflicts.OwnedWindows.OfType<ConfirmWindow>().Single();
        warning.Title.Should().Be(ConflictsDialog.AreYouSureYouWantDeleteFilesCaption);
        Click(warning, "ConfirmButton");

        WaitUntil(() => !conflicts.IsVisible);
        WaitUntil(() => !File.Exists(Path.Combine(_repo.Path, ".git", "MERGE_HEAD")));
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().Be("ours");
    }

    [AvaloniaTest]
    public void Open_in_mergetool_lists_gits_merge_tools_the_first_in_bold()
    {
        MergeConflict(theirs: "theirs");

        ConflictsWindow conflicts = Open();

        List<MenuItem> tools = [.. Find<MenuItem>(conflicts, "CustomMergeToolMenuItem").Items.OfType<MenuItem>()];
        tools.Select(item => (item.Header as TextBlock)?.Text).Should().Equal("kdiff3", "meld");
        tools.Select(item => item.FontWeight).Should().Equal(FontWeight.Bold, FontWeight.Normal);
        conflicts.Close();
    }

    [AvaloniaTest]
    public void A_submodule_conflict_opens_upstreams_submodule_window_and_stages_the_current_commit()
    {
        using TestRepository sub = new();
        (string baseCommit, string local, string remote) = SubmoduleConflict(sub);
        ConflictsWindow conflicts = Open();
        conflicts.Conflicts.Select(conflict => conflict.Filename).Should().Equal("sub");

        Click(conflicts, "MergeButton");
        WaitUntil(() => conflicts.OwnedWindows.OfType<MergeSubmoduleWindow>().Any());
        MergeSubmoduleWindow submodule = conflicts.OwnedWindows.OfType<MergeSubmoduleWindow>().Single();
        WaitUntil(() => submodule.SubmoduleLoaded.IsCompleted);

        Find<TextBlock>(submodule, "SubmoduleText").Text.Should().Be("sub");
        (Find<TextBox>(submodule, "BaseBox").Text, Find<TextBox>(submodule, "LocalBox").Text,
            Find<TextBox>(submodule, "RemoteBox").Text, Find<TextBox>(submodule, "CurrentBox").Text).Should()
            .Be((baseCommit, local, remote, local));
        Find<Button>(submodule, "CheckoutBranchButton").IsEnabled.Should().BeFalse("no checkout was given");
        Click(submodule, "StageCurrentButton");

        WaitUntil(() => conflicts.OwnedWindows.OfType<ConfirmWindow>().Any());
        _repo.Run("ls-files", "-u").Should().BeEmpty();
        _repo.Run("ls-files", "-s", "sub").Should().Contain(local);
        conflicts.Close();
    }

    [AvaloniaTest]
    public void Checkout_branch_in_a_conflicted_submodule_asks_for_a_branch_with_both_commits()
    {
        using TestRepository sub = new();
        (_, string local, string remote) = SubmoduleConflict(sub);
        string submodulePath = Path.Combine(_repo.Path, "sub");
        GitProcess.Run(submodulePath, "checkout", "-q", "-b", "both", local);
        GitProcess.Run(submodulePath, "-c", "user.name=Test", "-c", "user.email=test@example.com", "merge", "-q", "--no-edit",
            remote);
        string both = GitProcess.Run(submodulePath, "rev-parse", "HEAD").Trim();
        GitProcess.Run(submodulePath, "checkout", "-q", local);
        List<(string Path, IReadOnlyList<ObjectId> Commits)> checkouts = [];
        ConflictsWindow conflicts = Open((_, path, commits) =>
        {
            checkouts.Add((path, commits));
            GitProcess.Run(path, "checkout", "-q", "both");
            return Task.FromResult(true);
        });

        Click(conflicts, "MergeButton");
        WaitUntil(() => conflicts.OwnedWindows.OfType<MergeSubmoduleWindow>().Any());
        MergeSubmoduleWindow submodule = conflicts.OwnedWindows.OfType<MergeSubmoduleWindow>().Single();
        WaitUntil(() => submodule.SubmoduleLoaded.IsCompleted);
        Click(submodule, "CheckoutBranchButton");

        WaitUntil(() => conflicts.OwnedWindows.OfType<ConfirmWindow>().Any());
        checkouts.Should().ContainSingle();
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(checkouts[0].Path)).Should().Be(Path.GetFullPath(submodulePath));
        checkouts[0].Commits.Should().Equal(ObjectId.Parse(local), ObjectId.Parse(remote));
        _repo.Run("ls-files", "-s", "sub").Should().Contain(both);
        conflicts.Close();
    }

    // The submodule "sub" at sub's HEAD, moved to sibling commits on "other" (remote) and on the base branch (local), then
    // "other" merged: git cannot merge two commits that do not contain each other, so the submodule conflicts.
    private (string Base, string Local, string Remote) SubmoduleConflict(TestRepository sub)
    {
        string baseCommit = sub.Run("rev-parse", "HEAD").Trim();
        string remote = Commit(sub, "remote");
        sub.Run("checkout", "-q", "-b", "local", baseCommit);
        string local = Commit(sub, "local");
        _repo.Run("-c", "protocol.file.allow=always", "submodule", "add", "-q", sub.Path, "sub");
        string submodulePath = Path.Combine(_repo.Path, "sub");
        GitProcess.Run(submodulePath, "checkout", "-q", baseCommit);
        _repo.Run("commit", "-q", "-am", "add sub");

        _repo.Run("checkout", "-q", "-b", "other");
        GitProcess.Run(submodulePath, "checkout", "-q", remote);
        _repo.Run("commit", "-q", "-am", "sub remote");
        _repo.Run("checkout", "-q", _base);
        GitProcess.Run(submodulePath, "checkout", "-q", local);
        _repo.Run("commit", "-q", "-am", "sub local");
        GitProcess.RunAllowingFailure(_repo.Path, "merge", "other");
        return (baseCommit, local, remote);

        static string Commit(TestRepository repository, string text)
        {
            File.WriteAllText(Path.Combine(repository.Path, text + ".txt"), text);
            repository.Run("add", text + ".txt");
            repository.Run("commit", "-q", "-m", text);
            return repository.Run("rev-parse", "HEAD").Trim();
        }
    }

    // "other" changes a.txt to theirs (or deletes it), the base branch to "ours"; then a merge of other, or a rebase onto it.
    private void MergeConflict(string? theirs, bool rebase = false)
    {
        _repo.Run("checkout", "-q", "-b", "other");
        if (theirs is null)
        {
            _repo.Run("rm", "-q", "a.txt");
            _repo.Run("commit", "-q", "-m", "delete");
        }
        else
        {
            File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), theirs);
            _repo.Run("commit", "-q", "-am", "theirs");
        }

        _repo.Run("checkout", "-q", _base);
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "ours");
        _repo.Run("commit", "-q", "-am", "ours");
        GitProcess.RunAllowingFailure(_repo.Path, rebase ? "rebase" : "merge", "other");
    }

    /// <summary>
    ///  The git program's full path, which a stand-in merge tool can name: upstream's merge tool check wants a file that
    ///  exists, by its exact name.
    /// </summary>
    public static string GitPath()
        => (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .SelectMany(folder => new[] { Path.Combine(folder, "git.exe"), Path.Combine(folder, "git") })
            .First(File.Exists);

    private ConflictsWindow Open(Func<Window, string, IReadOnlyList<ObjectId>, Task<bool>>? checkoutSubmoduleBranch = null)
    {
        Window owner = new();
        owner.Show();
        RepositoryViewModel repository = new(new GitRepositoryService());
        ConflictsWindow conflicts = new(_repo.Path, repository, new RepositoryOperationsViewModel(new GitOperations()),
            TestAppBuilder.Preferences, (_, _) => { }, _ => _commitsOffered++,
            checkoutSubmoduleBranch: checkoutSubmoduleBranch);
        _ = conflicts.ShowDialog(owner);
        WaitUntil(() => conflicts.ConflictsLoaded.IsCompleted);
        return conflicts;
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
