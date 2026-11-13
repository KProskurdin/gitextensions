using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

internal sealed class GitIgnoreWindowTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private TestRepository _repo = null!;
    private string _gitIgnore = "";

    [SetUp]
    public void SetUp()
    {
        _repo = new TestRepository();
        _gitIgnore = Path.Combine(_repo.Path, ".gitignore");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    [AvaloniaTest]
    public void Save_writes_the_edited_file_ended_with_a_line_ending()
    {
        File.WriteAllText(_gitIgnore, "bin/\n");
        (Window owner, GitIgnoreWindow window) = OpenEditor(localExclude: false);
        Editor(window).Text.Should().Be("bin/\n");
        window.Title.Should().Be("Edit .gitignore");

        Editor(window).Text = "bin/\nobj/";
        Click(window, "SaveButton");

        WaitUntil(() => !window.IsVisible);
        window.Saved.Should().BeTrue();
        File.ReadAllText(_gitIgnore).Should().Be("bin/\nobj/\n");
        owner.Close();
    }

    [AvaloniaTest]
    public void Add_default_ignores_adds_the_missing_defaults_unsaved()
    {
        File.WriteAllText(_gitIgnore, "*.log\n");
        (Window owner, GitIgnoreWindow window) = OpenEditor(localExclude: false);

        Click(window, "AddDefault");
        WaitUntil(() => Editor(window).Text != "*.log\n");

        string[] lines = (Editor(window).Text ?? "").Split('\n');
        lines.Should().Contain("Thumbs.db").And.Contain(".vs/");
        lines.Count(line => line == "*.log").Should().Be(1, "a default the file has is not added again");
        File.ReadAllText(_gitIgnore).Should().Be("*.log\n");
        window.Close();
        Answer(window, "NoButton");
        WaitUntil(() => !window.IsVisible);
        window.Saved.Should().BeFalse();
        owner.Close();
    }

    [AvaloniaTest]
    public void Closing_with_changes_asks_and_Yes_saves()
    {
        (Window owner, GitIgnoreWindow window) = OpenEditor(localExclude: false);
        Editor(window).Text = "obj/";

        window.Close();
        Answer(window, "CancelButton");
        window.IsVisible.Should().BeTrue("Cancel keeps the window open");

        window.Close();
        Answer(window, "YesButton");
        WaitUntil(() => !window.IsVisible);
        File.ReadAllText(_gitIgnore).Should().Be("obj/" + Environment.NewLine);
        owner.Close();
    }

    [AvaloniaTest]
    public void The_local_excludes_are_edited_in_the_git_folder()
    {
        (Window owner, GitIgnoreWindow window) = OpenEditor(localExclude: true);
        window.Title.Should().Be("Edit .git/info/exclude");

        Editor(window).Text = "/local.txt\n";
        Click(window, "SaveButton");

        WaitUntil(() => !window.IsVisible);
        File.ReadAllText(Path.Combine(_repo.Path, ".git", "info", "exclude")).Should().Be("/local.txt\n");
        File.Exists(_gitIgnore).Should().BeFalse();
        owner.Close();
    }

    [AvaloniaTest]
    public void Add_pattern_saves_opens_the_add_window_with_upstreams_pattern_and_reads_the_file_again()
    {
        (Window owner, GitIgnoreWindow window) = OpenEditor(localExclude: false);
        Editor(window).Text = "bin/\n";

        Click(window, "AddPattern");
        WaitUntil(() => window.OwnedWindows.OfType<AddToGitIgnoreWindow>().Any());
        File.ReadAllText(_gitIgnore).Should().Be("bin/\n");
        AddToGitIgnoreWindow add = window.OwnedWindows.OfType<AddToGitIgnoreWindow>().Single();
        Find<TextBox>(add, "FilePattern").Text.Should().Be(GitIgnoreDialog.ProposedPattern);
        Click(add, "AddToIgnore");

        WaitUntil(() => Editor(window).Text == "bin/\n*.dll\n");
        window.Close();
        WaitUntil(() => !window.IsVisible);
        window.Saved.Should().BeTrue();
        owner.Close();
    }

    [AvaloniaTest]
    public void The_add_window_previews_the_matched_files_and_appends_the_patterns()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "notes.txt"), "scratch");
        File.WriteAllText(_gitIgnore, "bin/");
        Window owner = new();
        owner.Show();
        AddToGitIgnoreWindow add = new(_repo.Path, localExclude: false, ["/notes.txt"]);
        _ = add.ShowDialog(owner);

        WaitUntil(() => add.PreviewFiles.Count == 1);
        add.PreviewFiles.Should().Equal("notes.txt");
        Find<TextBlock>(add, "_NO_TRANSLATE_filesWillBeIgnored").Text.Should().Be("1 file(s) matched");
        Find<Control>(add, "noMatchPanel").IsVisible.Should().BeFalse();

        Find<TextBox>(add, "FilePattern").Text = "*.nothing";
        WaitUntil(() => Find<Control>(add, "noMatchPanel").IsVisible);
        Find<TextBlock>(add, "_NO_TRANSLATE_filesWillBeIgnored").Text.Should().Be("0 file(s) matched");

        Find<TextBox>(add, "FilePattern").Text = "/notes.txt\n\n*.tmp";
        Click(add, "AddToIgnore");
        WaitUntil(() => !add.IsVisible);
        add.Added.Should().BeTrue();
        File.ReadAllText(_gitIgnore).Should().Be("bin/" + Environment.NewLine + "/notes.txt" + Environment.NewLine
                                                 + "*.tmp" + Environment.NewLine);
        owner.Close();
    }

    [AvaloniaTest]
    public void The_repository_menu_opens_the_editor()
    {
        MainWindow main = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        main.Show();
        main.OpenRepository(_repo.Path);
        WaitUntil(() => !main.IsBusy);

        UiClicks.Raise(main.FindControl<MenuItem>("GitIgnoreMenuItem")!, MenuItem.ClickEvent);
        WaitUntil(() => main.OwnedWindows.OfType<GitIgnoreWindow>().Any());
        main.OwnedWindows.OfType<GitIgnoreWindow>().Single().Close();
        main.Close();
    }

    private (Window Owner, GitIgnoreWindow Window) OpenEditor(bool localExclude)
    {
        Window owner = new();
        owner.Show();
        GitIgnoreWindow window = new(_repo.Path, localExclude);
        _ = window.ShowDialog(owner);
        WaitUntil(() => window.FileLoaded.IsCompleted);
        return (owner, window);
    }

    private static TextBox Editor(Window window) => Find<TextBox>(window, "_NO_TRANSLATE_GitIgnoreEdit");

    private static void Answer(Window window, string button)
    {
        WaitUntil(() => window.OwnedWindows.OfType<MessageBoxWindow>().Any());
        MessageBoxWindow box = window.OwnedWindows.OfType<MessageBoxWindow>().Single();
        Click(box, button);
        WaitUntil(() => !box.IsVisible);
    }

    private static void Click(Window window, string name)
        => UiClicks.Raise(Find<Button>(window, name), Button.ClickEvent);

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
