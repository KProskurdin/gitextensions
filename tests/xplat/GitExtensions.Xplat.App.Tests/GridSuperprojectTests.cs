using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  Upstream's grid in a submodule: the superproject's checkout, conflict and refs as dashed labels
///  (<c>MessageColumnProvider.DrawSuperprojectInfo</c> and <c>DrawSuperprojectRefs</c>).
/// </summary>
internal sealed class GridSuperprojectTests
{
    private const string SubmodulePath = "lib";
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _library = null!;
    private TestRepository _super = null!;

    private string Submodule => Path.Combine(_super.Path, SubmodulePath);

    [SetUp]
    public void Setup()
    {
        Reset();
        _library = new TestRepository();
        _super = new TestRepository();
        _super.Run("-c", "protocol.file.allow=always", "submodule", "add", "-q", _library.Path, SubmodulePath);
        _super.Run("commit", "-q", "-m", "add submodule");
    }

    [TearDown]
    public void TearDown()
    {
        Reset();
        _super.Dispose();
        _library.Dispose();
    }

    [AvaloniaTest]
    public void The_commit_the_superproject_records_gets_its_mark_and_the_superprojects_branches_are_dashed()
    {
        string branch = _super.Run("branch", "--show-current").Trim();
        _super.Run("branch", "topic");
        _super.Run("tag", "release");
        MainWindow window = OpenWindow(Submodule);
        ListBox commits = Find<ListBox>(window, "CommitList");
        CommitListItem Head() => Rows(commits).First(item => item.Row.Subject == "second");

        WaitUntil(() => Head().GridLabels.Any(label => label.IsSuperprojectCommit));

        Head().GridLabels.Where(label => label.InSuperproject).Select(label => (label.Text, label.Ref is null)).Should()
            .Equal(("", true), (branch, false), ("topic", true));
        Head().GridLabels[0].IsSuperprojectCommit.Should().BeTrue("upstream draws the recorded commit's mark first");
        Rows(commits).Single(item => item.Row.Subject == "first").GridLabels.Should().BeEmpty();

        // Upstream's "Show superproject tags" is off by default.
        Click(window, "ShowSuperprojectTagsMenuItem");

        WaitUntil(() => Head().GridLabels.Any(label => label.Text == "release"));
        TestAppBuilder.Preferences.ShowSuperprojectTags.Should().BeTrue();
        Find<MenuItem>(window, "ShowSuperprojectTagsMenuItem").IsChecked.Should().BeTrue();
        Head().GridLabels.Single(label => label.Text == "release").Should().Match<GridLabel>(label =>
            label.Kind == RefKind.Tag && label.InSuperproject && label.Ref == null);

        Click(window, "ShowSuperprojectBranchesMenuItem");

        WaitUntil(() => Head().GridLabels.All(label => label.Text != "topic"));
        Head().GridLabels.Single(label => label.Text == branch).InSuperproject.Should().BeFalse();
    }

    [AvaloniaTest]
    public void A_conflict_on_the_submodule_marks_its_base_local_and_remote_commits()
    {
        string branch = _super.Run("branch", "--show-current").Trim();
        string baseCommit = GitProcess.Run(Submodule, "rev-parse", "HEAD").Trim();
        _super.Run("checkout", "-q", "-b", "theirs");
        CommitInSubmodule("their change");
        _super.Run("checkout", "-q", branch);
        GitProcess.Run(Submodule, "checkout", "-q", baseCommit);
        CommitInSubmodule("our change");
        GitProcess.RunAllowingFailure(_super.Path, "merge", "-q", "--no-edit", "theirs").Should().NotBe(0);

        MainWindow window = OpenWindow(Submodule);
        ListBox commits = Find<ListBox>(window, "CommitList");
        string Marks(string subject) => string.Join(",", Rows(commits).Single(item => item.Row.Subject == subject)
            .GridLabels.Where(label => label.IsSuperprojectCommit).Select(label => label.Text));

        WaitUntil(() => Marks("second") == "Base");
        Marks("our change").Should().Be("Local");
        Marks("their change").Should().Be("Remote");
    }

    // Commits in the submodule, and records the commit in the superproject.
    private void CommitInSubmodule(string subject)
    {
        File.WriteAllText(Path.Combine(Submodule, subject + ".txt"), subject);
        GitProcess.Run(Submodule, "add", ".");
        GitProcess.Run(Submodule, "-c", "user.name=Test", "-c", "user.email=test@example.com", "commit", "-q", "-m",
            subject);
        _super.Run("add", SubmodulePath);
        _super.Run("commit", "-q", "-m", "lib: " + subject);
    }

    private static void Reset()
    {
        TestAppBuilder.Preferences.ShowSuperprojectBranches = true;
        TestAppBuilder.Preferences.ShowSuperprojectRemoteBranches = false;
        TestAppBuilder.Preferences.ShowSuperprojectTags = false;
    }

    private static MainWindow OpenWindow(string path)
    {
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(path);
        WaitUntil(() => !window.IsBusy);
        return window;
    }

    private static IReadOnlyList<CommitListItem> Rows(ListBox commits)
        => commits.ItemsSource as IReadOnlyList<CommitListItem> ?? [];

    private static void Click(Window window, string name)
        => Find<MenuItem>(window, name).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

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
