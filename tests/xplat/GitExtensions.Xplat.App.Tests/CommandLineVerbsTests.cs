using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using GitCommands;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.App.Tests;

// Upstream's GitUICommands.RunCommand: each verb's window on its own, and upstream's exit codes.
internal sealed class CommandLineVerbsTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TestRepository _repo = null!;
    private string _outside = null!;
    private int? _exitCode;

    [SetUp]
    public void Setup()
    {
        _repo = new TestRepository();
        _outside = Directory.CreateTempSubdirectory("xplat-verbs-outside-").FullName;
        _exitCode = null;
    }

    [TearDown]
    public void TearDown()
    {
        _repo.Dispose();
        Directory.Delete(_outside, recursive: true);
    }

    [AvaloniaTest]
    public void A_folder_alone_opens_the_browse_window_on_it()
    {
        CommandLineVerbs.OpensBrowseWindow(Request(_repo.Path), _outside, out string? repository).Should().BeTrue();
        repository.Should().Be(_repo.Path);
    }

    [AvaloniaTest]
    public void A_file_alone_opens_the_browse_window_on_its_folder()
    {
        string file = Path.Combine(_repo.Path, "notes.txt");
        File.WriteAllText(file, "");

        CommandLineVerbs.OpensBrowseWindow(Request(file), _outside, out string? repository).Should().BeTrue();
        repository.Should().Be(_repo.Path);
    }

    [AvaloniaTest]
    public void Browse_opens_the_browse_window_on_the_repository_of_its_path()
    {
        CommandLineVerbs.OpensBrowseWindow(Request("browse", _repo.Path), _outside, out string? repository).Should().BeTrue();
        repository.Should().Be(_repo.Path + Path.DirectorySeparatorChar);
    }

    [AvaloniaTest]
    public void Openrepo_opens_the_repository_named_in_its_file()
    {
        string file = Path.Combine(_outside, "repo.txt");
        File.WriteAllText(file, _repo.Path + "\nignored\n");

        CommandLineVerbs.OpensBrowseWindow(Request("openrepo", file), _outside, out string? repository).Should().BeTrue();
        repository.Should().Be(_repo.Path);
    }

    [AvaloniaTest]
    [TestCase("settings")]
    [TestCase("help")]
    [TestCase("no-such-folder")]
    public void Verbs_and_unknown_words_do_not_open_the_browse_window(string argument)
        => CommandLineVerbs.OpensBrowseWindow(Request(argument), _outside, out _).Should().BeFalse();

    [AvaloniaTest]
    public void A_verb_without_its_file_shows_upstreams_message_and_fails()
    {
        MessageBoxWindow message = Open<MessageBoxWindow>(Request("blame"), _outside);

        message.ViewModel.Text.Should().Be("Cannot open blame, there is no file selected.");
        message.ViewModel.Title.Should().Be("Blame");
        CloseAndExitCode(message).Should().Be(CommandLine.FailedExitCode);
    }

    [AvaloniaTest]
    public void An_unknown_word_with_arguments_shows_upstreams_usage_and_succeeds()
    {
        CommandLineHelpWindow usage = Open<CommandLineHelpWindow>(Request("help", "me"), _outside);

        usage.Title.Should().Be(CommandLine.UsageCaption);
        usage.FindControl<SelectableTextBlock>("_NO_TRANSLATE_commands")!.Text.Should().Be(CommandLine.UsageCommands);
        CloseAndExitCode(usage).Should().Be(0);
    }

    [AvaloniaTest]
    [TestCase("checkoutrevision", typeof(CheckoutRevisionWindow))]
    [TestCase("tag", typeof(CreateTagWindow))]
    [TestCase("branch", typeof(CreateBranchWindow))]
    [TestCase("cherry", typeof(CherryPickWindow))]
    public void A_dialog_verb_shows_over_an_unseen_browse_window_and_its_cancel_fails(string verb, Type type)
    {
        MainWindow host = Open<MainWindow>(Request(verb), _repo.Path);
        host.ShowInTaskbar.Should().BeFalse();
        host.Width.Should().Be(1);

        Window dialog = WaitForOwned(host, type);
        dialog.Close();

        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(CommandLine.FailedExitCode, "a cancelled dialog is upstream's false");
    }

    [AvaloniaTest]
    [TestCase("stash", typeof(StashWindow))]
    [TestCase("viewdiff", typeof(LogWindow))]
    [TestCase("commit", typeof(CommitWindow))]
    [TestCase("pull", typeof(PullWindow))]
    public void A_window_verb_ends_with_its_window(string verb, Type type)
    {
        MainWindow host = Open<MainWindow>(Request(verb), _repo.Path);

        WaitForOwned(host, type).Close();

        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(0);
    }

    [AvaloniaTest]
    public void Blame_and_file_history_open_on_the_file_given()
    {
        string file = Path.Combine(_repo.Path, "a.txt");
        MainWindow blame = Open<MainWindow>(Request("blame", file), _outside);
        WaitForOwned(blame, typeof(BlameWindow)).Close();
        WaitUntil(() => _exitCode is not null);

        _exitCode = null;
        MainWindow history = Open<MainWindow>(Request("filehistory", file), _outside);
        WaitForOwned(history, typeof(FileHistoryWindow)).Close();
        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(0);
    }

    [AvaloniaTest]
    public void Reset_with_files_resets_them_and_deletes_the_new_ones_when_asked()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "a.txt"), "changed");
        File.WriteAllText(Path.Combine(_repo.Path, "new.txt"), "new");
        File.WriteAllText(Path.Combine(_repo.Path, "kept.txt"), "kept");
        MainWindow host = Open<MainWindow>(Request("reset", "a.txt", "new.txt"), _repo.Path);

        ResetChangesWindow question = (ResetChangesWindow)WaitForOwned(host, typeof(ResetChangesWindow));
        question.FindControl<CheckBox>("DeleteNewFilesCheck")!.IsChecked = true;
        UiClicks.Raise(question.FindControl<Button>("ResetButton")!, Button.ClickEvent);

        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(0);
        File.Exists(Path.Combine(_repo.Path, "new.txt")).Should().BeFalse("upstream deletes the new files when asked");
        File.ReadAllText(Path.Combine(_repo.Path, "a.txt")).Should().NotBe("changed");
        File.Exists(Path.Combine(_repo.Path, "kept.txt")).Should().BeTrue("only the files given are reset");
    }

    [AvaloniaTest]
    public void Commit_quiet_with_nothing_to_commit_opens_no_window_and_succeeds()
    {
        _repo.Run("status", "--porcelain").Should().BeEmpty();

        Open<MainWindow>(Request("commit", "--quiet"), _repo.Path);

        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(0);
    }

    [AvaloniaTest]
    public void Init_opens_upstreams_dialog_on_the_folder_given_and_creates_the_repository()
    {
        string folder = Path.Combine(_outside, "new");
        MainWindow host = Open<MainWindow>(Request("init", folder), _outside);

        InitRepositoryWindow init = (InitRepositoryWindow)WaitForOwned(host, typeof(InitRepositoryWindow));
        init.ViewModel.Directory.Should().Be(folder);
        init.ViewModel.CreateCommand.Execute(null);
        WaitForOwned(init, typeof(MessageBoxWindow)).Close();

        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(0);
        Directory.Exists(Path.Combine(folder, ".git")).Should().BeTrue();
    }

    [AvaloniaTest]
    public void Commit_with_a_message_starts_the_commit_window_on_it()
    {
        MainWindow host = Open<MainWindow>(Request("commit", "--message", "From the command line"), _repo.Path);

        CommitWindow commit = (CommitWindow)WaitForOwned(host, typeof(CommitWindow));
        commit.ViewModel.Message.Should().Be("From the command line");
        commit.Close();
        WaitUntil(() => _exitCode is not null);
    }

    [AvaloniaTest]
    [TestCase("not-a-commit")]
    [TestCase("HEAD", "--other")]
    public void File_history_with_a_revision_that_is_no_commit_id_or_another_option_fails(params string[] after)
    {
        Open<MainWindow>(Request(["filehistory", Path.Combine(_repo.Path, "a.txt"), .. after]), _outside);

        WaitUntil(() => _exitCode is not null);
        _exitCode.Should().Be(CommandLine.FailedExitCode);
    }

    [AvaloniaTest]
    public async Task Browse_selects_both_commits_of_commit_and_a_path_filter_is_upstreams_file_history_mode()
    {
        string first = _repo.Run("rev-parse", "HEAD~1").Trim();
        string second = _repo.Run("rev-parse", "HEAD").Trim();
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = window.FindControl<ListBox>("CommitList")!;

        await window.ApplyBrowseArgumentsAsync(Request("browse", "--pathFilter=a.txt", $"-commit={second},{first}"));

        WaitUntil(() => !window.IsBusy && commits.SelectedItems!.Count == 2);
        commits.SelectedItems!.OfType<CommitListItem>().Select(item => item.Row.Hash).Should().BeEquivalentTo([first, second]);
        window.FindControl<ToggleButton>("ToggleLeftPanelButton")!.IsChecked.Should().BeFalse("the left panel collapses");
    }

    [AvaloniaTest]
    public void The_editor_starts_on_the_line_asked_for()
    {
        string file = Path.Combine(_outside, "lines.txt");
        File.WriteAllText(file, "one\ntwo\nthree\n");
        EditorWindow editor = new(file, lineNumber: 3);
        editor.Show();
        AvaloniaEdit.TextEditor content = editor.FindControl<AvaloniaEdit.TextEditor>("ContentBox")!;
        WaitUntil(() => content.Text?.Contains("three", StringComparison.Ordinal) == true && editor.FileLoaded.IsCompleted);

        content.TextArea.Caret.Line.Should().Be(3);
        editor.Close();
    }

    [AvaloniaTest]
    public async Task A_hosted_verb_leaves_the_recent_repositories_alone()
    {
        int before = (await AppServices.RecentRepositories.LoadAsync()).Count;
        MainWindow host = Open<MainWindow>(Request("stash"), _repo.Path);

        WaitForOwned(host, typeof(StashWindow)).Close();
        WaitUntil(() => _exitCode is not null);

        (await AppServices.RecentRepositories.LoadAsync()).Should().HaveCount(before);
    }

    [AvaloniaTest]
    public async Task Browse_applies_its_path_filter_and_selects_its_commit()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "b.txt"), "b");
        _repo.Run("add", "b.txt");
        _repo.Run("commit", "-q", "-m", "third");
        string first = _repo.Run("rev-parse", "HEAD~2").Trim();
        MainWindow window = new(new GitDiscoveryResult(GitDiscoveryStatus.Found, "git", Version: null));
        window.Show();
        window.OpenRepository(_repo.Path);
        WaitUntil(() => !window.IsBusy);
        ListBox commits = window.FindControl<ListBox>("CommitList")!;

        await window.ApplyBrowseArgumentsAsync(Request("browse", "--pathFilter=a.txt", $"-commit={first[..10]}"));

        WaitUntil(() => !window.IsBusy && commits.SelectedItem is CommitListItem { Row.Subject: "first" });
        commits.Items.OfType<CommitListItem>().Select(item => item.Row.Subject).Should().NotContain("third");
    }

    [AvaloniaTest]
    public void Only_difftool_with_a_file_and_uninstall_open_no_window()
    {
        CommandLineVerbs.OpensNoWindow(Request("uninstall")).Should().BeTrue();
        CommandLineVerbs.OpensNoWindow(Request("difftool", "a.txt")).Should().BeTrue();
        CommandLineVerbs.OpensNoWindow(Request("difftool")).Should().BeFalse("upstream's message says the file is missing");
        CommandLine.Verbs.Where(verb => verb is not ("clone" or "fileeditor")).Should()
            .OnlyContain(verb => CommandLineVerbs.IsPorted(verb), "every verb runs now");
    }

    [AvaloniaTest]
    public void A_repository_verb_outside_a_repository_says_so_and_fails()
    {
        MessageBoxWindow message = Open<MessageBoxWindow>(Request("remotes"), _outside);

        message.ViewModel.Text.Should().Be($"\"{_outside}\" is not in a git repository.");
        CloseAndExitCode(message).Should().Be(CommandLine.FailedExitCode);
    }

    [AvaloniaTest]
    public void About_and_settings_open_without_a_repository()
    {
        CloseAndExitCode(Open<AboutWindow>(Request("about"), _outside)).Should().Be(0);
        CloseAndExitCode(Open<SettingsWindow>(Request("settings"), _outside)).Should()
            .Be(CommandLine.FailedExitCode, "upstream's code says the settings were not saved");
    }

    [AvaloniaTest]
    [TestCase("cleanup", typeof(CleanupRepositoryWindow))]
    [TestCase("formatpatch", typeof(FormatPatchWindow))]
    [TestCase("gitignore", typeof(GitIgnoreWindow))]
    [TestCase("remotes", typeof(RemotesWindow))]
    [TestCase("viewpatch", typeof(ViewPatchWindow))]
    [TestCase("searchfile", typeof(SearchWindow))]
    public void A_repository_verb_opens_its_window_in_the_repository_around_the_current_directory(string verb, Type type)
    {
        Window window = CommandLineVerbs.Open(Request(verb), _repo.Path, git: null, code => _exitCode = code);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.Should().BeOfType(type);
        window.Close();
        WaitUntil(() => _exitCode is not null);
    }

    [AvaloniaTest]
    public void Searchfile_prints_the_file_taken_with_the_repositorys_path_and_fails_without_one()
    {
        File.WriteAllText(Path.Combine(_repo.Path, "found.txt"), "x");
        _repo.Run("add", "found.txt");
        _repo.Run("commit", "-q", "-m", "found");
        SearchWindow search = Open<SearchWindow>(Request("searchfile"), _repo.Path);
        StringWriter output = new();
        TextWriter console = Console.Out;
        Console.SetOut(output);
        try
        {
            TextBox box = search.FindControl<TextBox>("txtSearchBox")!;
            box.Focus();
            box.Text = "found";
            Dispatcher.UIThread.RunJobs();
            WaitUntil(() => search.Searched.IsCompleted);
            search.ViewModel.Candidates.Should().Equal(["found.txt"]);
            Dispatcher.UIThread.RunJobs();
            search.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
            WaitUntil(() => _exitCode is not null);
        }
        finally
        {
            Console.SetOut(console);
        }

        _exitCode.Should().Be(0);
        output.ToString().Trim().Should().Be(Path.Combine(_repo.Path, "found.txt"));

        _exitCode = null;
        CloseAndExitCode(Open<SearchWindow>(Request("searchfile"), _repo.Path)).Should().Be(CommandLine.FailedExitCode);
    }

    private static CommandLineRequest Request(params string[] args) => CommandLine.Parse(args)!;

    private T Open<T>(CommandLineRequest request, string currentDirectory)
        where T : Window
    {
        Window window = CommandLineVerbs.Open(request, currentDirectory, git: null, code => _exitCode = code);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window.Should().BeOfType<T>().Subject;
    }

    private static Window WaitForOwned(Window host, Type type)
    {
        WaitUntil(() => host.OwnedWindows.Any(type.IsInstanceOfType));
        return host.OwnedWindows.First(type.IsInstanceOfType);
    }

    private int? CloseAndExitCode(Window window)
    {
        _exitCode = null;
        window.Close();
        WaitUntil(() => _exitCode is not null);
        return _exitCode;
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
            Thread.Sleep(20);
        }
    }
}
