using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileListMenuActionsTests
{
    private static readonly ObjectId Parent = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");
    private static readonly ObjectId Commit = ObjectId.Parse("1234567812345678123456781234567812345678");

    private string _workingDir = null!;
    private InMemoryAppPreferences _preferences = null!;
    private RecordingHost _host = null!;
    private FakeGitOperations _git = null!;
    private DiffMenuSelection? _selection;
    private int _refreshes;

    [SetUp]
    public void Setup()
    {
        _workingDir = Path.Combine(Path.GetTempPath(), "xplat-menu-actions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDir);
        _preferences = new InMemoryAppPreferences();
        _host = new RecordingHost();
        _git = new FakeGitOperations();
        _selection = null;
        _refreshes = 0;
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_workingDir, recursive: true);

    [Test]
    public async Task Delete_should_ask_then_unstage_the_staged_files_and_delete_them_from_the_disk()
    {
        File.WriteAllText(Path.Combine(_workingDir, "a.txt"), "a");
        _selection = new DiffMenuSelection(ObjectId.WorkTreeId, ObjectId.IndexId, [],
            [new DiffMenuFile("A", "a.txt", null, IsTracked: true)]);
        FileListMenuViewModel viewModel = NewViewModel();

        Task delete = viewModel.DeleteAsync();
        await WaitUntilAsync(() => _git.Count == 1);
        _git.Complete(0);
        await delete;

        _host.Questions.Should().Equal("Are you sure you want to delete the selected file(s)?");
        _git.NameAt(0).Should().Be("Unstage");
        File.Exists(Path.Combine(_workingDir, "a.txt")).Should().BeFalse();
        _refreshes.Should().Be(1);
    }

    [Test]
    public async Task Delete_answered_no_should_leave_the_files()
    {
        File.WriteAllText(Path.Combine(_workingDir, "a.txt"), "a");
        _selection = WorkTreeSelection("a.txt");
        _host.Answer = false;
        FileListMenuViewModel viewModel = NewViewModel();

        await viewModel.DeleteAsync();

        File.Exists(Path.Combine(_workingDir, "a.txt")).Should().BeTrue();
        _refreshes.Should().Be(0);
    }

    [Test]
    public async Task Stage_should_use_the_windows_own_stage_when_it_gave_one_else_stage_the_working_directorys_files()
    {
        _selection = WorkTreeSelection("a.txt", "b.txt");
        int ownStages = 0;
        FileListMenuViewModel own = NewViewModel(source => source with { Stage = () => ownStages++ });
        FileListMenuViewModel menus = NewViewModel();

        await own.StageCommand.ExecuteAsync(null);
        Task stage = menus.StageCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.Complete(0);
        await stage;

        ownStages.Should().Be(1);
        own.HasOwnStage.Should().BeTrue();
        _git.NameAt(0).Should().Be("Stage");
        _git.ArgumentsAt(0).Should().Be($"{_workingDir} a.txt,b.txt");
        _refreshes.Should().Be(1);
    }

    [Test]
    public async Task Move_should_ask_again_for_a_name_git_does_not_take_and_stop_when_cancelled()
    {
        _selection = WorkTreeSelection("a.txt");
        _host.Names.Enqueue("a.txt");
        _host.Names.Enqueue(null);
        FileListMenuViewModel viewModel = NewViewModel();

        await viewModel.MoveAsync();

        _host.NameQuestions.Should().Equal("a.txt", "a.txt");
        _refreshes.Should().Be(0, "nothing was moved");
    }

    [Test]
    public async Task Stop_tracking_should_tell_upstreams_error_when_git_fails()
    {
        _selection = WorkTreeSelection("a.txt");
        FileListMenuViewModel viewModel = NewViewModel();

        Task stop = viewModel.StopTrackingCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _git.Count == 1);
        _git.Fail(0, new InvalidOperationException("fatal"));
        await stop;

        _git.NameAt(0).Should().Be("StopTracking");
        _host.Errors.Should().Equal("Fail to stop tracking the file 'a.txt'.");
        _refreshes.Should().Be(1);
    }

    [Test]
    public async Task The_submodule_items_should_act_on_the_selected_submodules_once_each()
    {
        _selection = new DiffMenuSelection(Parent, Commit, [Parent],
        [
            new DiffMenuFile("M", "sub", null, IsTracked: true) { IsSubmodule = true },
            new DiffMenuFile("M", "a.txt", null, IsTracked: true),
            new DiffMenuFile("M", "sub", null, IsTracked: true) { IsSubmodule = true },
        ]);
        List<string> updated = [];
        FileListSubmodules submodules = new(_ => Task.CompletedTask, paths =>
        {
            updated.AddRange(paths);
            return Task.CompletedTask;
        }, _ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);
        FileListMenuViewModel viewModel = NewViewModel(source => source with { Submodules = () => submodules });

        await viewModel.UpdateSubmoduleCommand.ExecuteAsync(null);

        updated.Should().Equal("sub");
    }

    [Test]
    public void File_history_and_blame_should_open_the_history_unless_the_window_gave_its_own_blame()
    {
        _selection = new DiffMenuSelection(Parent, Commit, [Parent], [new DiffMenuFile("M", "a.txt", null, IsTracked: true)]);
        int ownBlames = 0;
        FileListMenuViewModel history = NewViewModel(source => source with { HasFileHistory = () => true });
        FileListMenuViewModel blame = NewViewModel(source => source with { HasFileHistory = () => true, Blame = () => ownBlames++ });

        history.FileHistoryCommand.Execute(null);
        history.BlameCommand.Execute(null);
        blame.BlameCommand.Execute(null);

        _host.Histories.Should().Equal($"a.txt {Commit} blame=False", $"a.txt {Commit} blame=True");
        ownBlames.Should().Be(1);
    }

    [Test]
    public void A_double_click_should_show_a_files_history()
    {
        _selection = new DiffMenuSelection(Parent, Commit, [Parent], [new DiffMenuFile("M", "a.txt", null, IsTracked: true)]);
        FileListMenuViewModel viewModel = NewViewModel(source => source with { HasFileHistory = () => true });

        viewModel.RunDoubleClick().Should().BeTrue();

        _host.Histories.Should().ContainSingle();
    }

    [Test]
    public async Task Find_file_should_select_the_file_taken_in_the_search_window()
    {
        _selection = WorkTreeSelection("a.txt");
        string? selected = null;
        FileListFind find = new(() => [("src/a.cs", null), ("b.txt", null)], path => selected = path);
        _host.FoundFile = "src/a.cs";
        FileListMenuViewModel viewModel = NewViewModel(source => source with { FindFile = find });

        await viewModel.FindFileAsync();

        selected.Should().Be("src/a.cs");
    }

    [Test]
    public void Show_find_in_commit_files_should_toggle_the_box_and_keep_the_choice()
    {
        _selection = WorkTreeSelection("a.txt");
        FakeGitGrep grep = new();
        FileListMenuViewModel viewModel = NewViewModel(source => source with { GitGrep = grep });

        viewModel.ToggleGitGrepBoxCommand.Execute(null);
        viewModel.OpenGitGrepCommand.Execute(null);

        grep.BoxVisible.Should().BeTrue();
        _preferences.ShowFindInCommitFilesGitGrep.Should().BeTrue();
        grep.Opened.Should().Be(1);
    }

    [Test]
    public void Show_in_file_tree_and_filter_in_grid_should_take_the_selected_folder_before_the_files()
    {
        _selection = new DiffMenuSelection(null, Commit, [Parent], [new DiffMenuFile("", "docs/a.md", null, IsTracked: true)],
            Folder: "docs", TreeFiles: true);
        List<string> shown = [];
        List<string> filtered = [];
        FileListMenuViewModel viewModel = NewViewModel(source => source with
        {
            ShowInFileTree = shown.Add,
            FilterFileInGrid = filtered.Add,
        });

        viewModel.ShowInFileTreeCommand.Execute(null);
        viewModel.FilterFileInGridCommand.Execute(null);

        shown.Should().Equal("docs");
        filtered.Should().ContainSingle().Which.Should().Contain("docs");
    }

    private static DiffMenuSelection WorkTreeSelection(params string[] files)
        => new(ObjectId.IndexId, ObjectId.WorkTreeId, [],
            [.. files.Select(file => new DiffMenuFile("M", file, null, IsTracked: true))]);

    private FileListMenuViewModel NewViewModel(Func<FileListMenuSource, FileListMenuSource>? change = null)
    {
        FileListMenuSource source = new()
        {
            RepositoryPath = () => _workingDir,
            Selection = () => _selection,
            DescribeRevision = id => id.ToShortString(),
            Refresh = () => _refreshes++,
            Actions = () => new RepositoryOperationsViewModel(_git),
        };
        FileListMenuViewModel viewModel = new(_preferences, isWindows: false);
        viewModel.Bind(change?.Invoke(source) ?? source, _host);
        return viewModel;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Timed out");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeGitGrep : IFileListGitGrep
    {
        public bool BoxVisible { get; private set; }

        public int Opened { get; private set; }

        public void SetBoxVisible(bool visible) => BoxVisible = visible;

        public void ShowWindow(string text) => Opened++;
    }

    private sealed class RecordingHost : IFileListMenuHost
    {
        public List<string> Errors { get; } = [];

        public List<string> Questions { get; } = [];

        public List<string> NameQuestions { get; } = [];

        public Queue<string?> Names { get; } = [];

        public List<string> Histories { get; } = [];

        public bool Answer { get; set; } = true;

        public string? FoundFile { get; set; }

        public string Translate(string english) => english;

        public void ShowError(string message) => Errors.Add(message);

        public Task ShowMoveErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }

        public Task<ResetChangesAction> AskResetAsync(bool hasExistingFiles, bool hasNewFiles, string question)
            => Task.FromResult(ResetChangesAction.Cancel);

        public Task<string?> AskNewNameAsync(string initialValue)
        {
            NameQuestions.Add(initialValue);
            return Task.FromResult(Names.Dequeue());
        }

        public Task<bool> ConfirmAsync(string text, string caption)
        {
            Questions.Add(text);
            return Task.FromResult(Answer);
        }

        public Task<string?> PickSaveFileAsync(string suggestion) => Task.FromResult<string?>(null);

        public Task<string?> PickFolderAsync(string start) => Task.FromResult<string?>(null);

        public Task OpenFileAsync(string path) => Task.CompletedTask;

        public void OpenWith(string path)
        {
        }

        public void EditFile(string path)
        {
        }

        public Task OpenInVisualStudioAsync(string path) => Task.CompletedTask;

        public void OpenFolder(string folder)
        {
        }

        public void ShowFileHistory(string repositoryPath, string file, string? revision, bool showBlame)
            => Histories.Add($"{file} {revision} blame={showBlame}");

        public Task<string?> FindFileAsync(Func<string, IEnumerable<string>> candidates) => Task.FromResult(FoundFile);

        public Task<bool> AddToIgnoreAsync(string repositoryPath, bool localExclude, IReadOnlyList<string> patterns)
            => Task.FromResult(false);

        public Task CherryPickAsync(string repositoryPath, (string Text, int Start, int Length) range, string status)
            => Task.CompletedTask;

        public Task RunInteractivePatchAsync(string repositoryPath, string path, bool reset) => Task.CompletedTask;

        public Task SetClipboardAsync(string text) => Task.CompletedTask;
    }
}
