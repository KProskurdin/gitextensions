using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class PatchWindowsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _output = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _output = Directory.CreateDirectory(_repo.Path.TrimEnd('/', '\\') + "-patches").FullName;
        TestAppBuilder.Preferences.LastFormatPatchDir = "";
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
        GitProcess.DeleteFolder(_output);
    }

    [AvaloniaTest]
    public void Format_patch_writes_the_selected_commit_and_keeps_the_folder()
    {
        Window owner = Owner();
        FormatPatchWindow window = new(_repo.Path, TestAppBuilder.Preferences);
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.CommitsLoaded.IsCompleted);
        Find<TextBlock>(window, "SelectedBranch").Text.Should()
            .Be("Current branch: " + _repo.Run("rev-parse", "--abbrev-ref", "HEAD").Trim());

        Click(window, "FormatPatch");
        MessageBoxWindow error = WaitForMessage(window);
        Find<SelectableTextBlock>(error, "MessageText").Text.Should().Be("You need to enter an output path.");
        error.Close();

        Find<TextBox>(window, "OutputPath").Text = _output;
        Dispatcher.UIThread.RunJobs();
        ListBox grid = Find<ListBox>(window, "RevisionGrid");
        grid.SelectedItems!.Add(grid.Items.Cast<CommitListItem>().Single(item => item.Row.Subject == "second"));
        Click(window, "FormatPatch");

        MessageBoxWindow result = WaitForMessage(window);
        result.Title.Should().Be("Patch result");
        result.Close();
        WaitUntil(() => !window.IsVisible);
        Directory.GetFiles(_output).Select(Path.GetFileName).Should().Equal("0001-second.patch");
        TestAppBuilder.Preferences.LastFormatPatchDir.Should().Be(_output);
        owner.Close();
    }

    [AvaloniaTest]
    public void View_patch_lists_the_file_patches_and_shows_the_selected_one()
    {
        string patch = FormatHead();
        Window owner = Owner();
        ViewPatchWindow window = new(_repo.Path, patch);
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();

        window.Patches.Select(file => file.FileNameB).Should().Equal("a.txt");
        Find<ListBox>(window, "GridChangedFiles").SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();

        window.FindControl<DiffView>("ChangesList")!.FindControl<TextBlock>("TitleText")!.Text.Should().Be("a.txt");
        owner.Close();
    }

    [AvaloniaTest]
    public void Apply_patch_applies_a_mail_and_closes()
    {
        string patch = FormatHead();
        _repo.Run("reset", "-q", "--hard", "HEAD~1");
        (Window owner, ApplyPatchWindow window) = OpenApply();
        window.SetPatchFile(patch);

        Click(window, "Apply");

        WaitUntil(() => !window.IsVisible);
        _repo.Run("log", "-1", "--format=%s").Trim().Should().Be("second");
        owner.Close();
    }

    [AvaloniaTest]
    public void A_stopped_git_am_offers_Solve_conflicts_Skip_and_Abort()
    {
        string patch = FormatHead();
        _repo.Run("reset", "-q", "--hard", "HEAD~1");
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "other");
        _repo.Run("commit", "-q", "-am", "other change");
        (Window owner, ApplyPatchWindow window) = OpenApply();
        window.SetPatchFile(patch);

        Click(window, "Apply");

        WaitUntil(() => window.Refreshed.IsCompleted && Find<Button>(window, "Abort").IsEnabled);
        window.IsVisible.Should().BeTrue("git am stopped");
        Find<Button>(window, "Apply").IsEnabled.Should().BeFalse();
        Find<Button>(window, "Skip").IsEnabled.Should().BeTrue();
        // Upstream applies mails with --3way, so the failed patch leaves conflicts: Solve conflicts is the default.
        Find<Button>(window, "Mergetool").IsDefault.Should().BeTrue();
        Find<AccessText>(window, "MergetoolText").Text.Should().Be(">_Solve conflicts<");
        Find<Button>(window, "SolveMergeConflicts").IsVisible.Should().BeTrue();
        Find<Button>(window, "Resolved").IsEnabled.Should().BeFalse();
        Find<ListBox>(window, "PatchGrid").ItemCount.Should().Be(1);

        Click(window, "Abort");

        WaitUntil(() => window.Refreshed.IsCompleted && Find<Button>(window, "Apply").IsEnabled);
        Directory.Exists(Path.Combine(_repo.Path, ".git", "rebase-apply")).Should().BeFalse();
        owner.Close();
    }

    [AvaloniaTest]
    public void Add_files_adds_the_filter()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        Window owner = Owner();
        AddFilesWindow window = new("new.txt", RunGit);
        Task<bool> result = window.ShowDialog<bool>(owner);

        Click(window, "AddFiles");

        WaitUntil(() => result.IsCompleted);
        _repo.Run("diff", "--cached", "--name-only").Trim().Should().Be("new.txt");
        owner.Close();
    }

    private string FormatHead()
    {
        GitModules.Open(_repo.Path).FormatPatch(_repo.Run("rev-parse", "HEAD~1").Trim(), "HEAD", _output);
        return Directory.GetFiles(_output).Single();
    }

    private (Window Owner, ApplyPatchWindow Window) OpenApply()
    {
        Window owner = Owner();
        ApplyPatchWindow window = new(_repo.Path, TestAppBuilder.Preferences, RunGit, null, null);
        _ = window.ShowDialog(owner);
        Dispatcher.UIThread.RunJobs();
        return (owner, window);
    }

    private Task<bool> RunGit(string arguments)
        => Task.Run(() => GitModules.Open(_repo.Path).GitExecutable
            .Execute((ArgumentString)arguments, throwOnErrorExit: false).ExitedSuccessfully);

    private static Window Owner()
    {
        Window owner = new();
        owner.Show();
        return owner;
    }

    private static MessageBoxWindow WaitForMessage(Window owner)
    {
        WaitUntil(() => owner.OwnedWindows.OfType<MessageBoxWindow>().Any());
        return owner.OwnedWindows.OfType<MessageBoxWindow>().Single();
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
