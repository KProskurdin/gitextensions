using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class FileListMenuViewModelTests
{
    private static readonly ObjectId Parent = ObjectId.Parse("abcdefabcdefabcdefabcdefabcdefabcdefabcd");
    private static readonly ObjectId Commit = ObjectId.Parse("1234567812345678123456781234567812345678");
    private InMemoryAppPreferences _preferences = null!;
    private string _workingDir = null!;

    [SetUp]
    public void Setup()
    {
        _preferences = new InMemoryAppPreferences();

        // The menu offers to open a working directory file that is there.
        _workingDir = Path.Combine(Path.GetTempPath(), "xplat-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDir);
        File.WriteAllText(Path.Combine(_workingDir, "new.txt"), "new");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_workingDir, recursive: true);

    [Test]
    public void Without_a_selection_only_the_tree_items_should_show()
    {
        FileListMenuViewModel viewModel = new(_preferences, isWindows: true);
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.Refresh(null, null, new FileListMenuContext(HasFileHistory: true),
            new FileListTreeState(HasSubnodes: true, AnyRootExpanded: true));

        changed.Should().Equal(string.Empty);
        viewModel.HasSelection.Should().BeFalse();
        viewModel.AreTreeItemsVisible.Should().BeTrue();
        viewModel.IsCollapseRootFoldersVisible.Should().BeTrue();
        viewModel.IsStageVisible.Should().BeFalse();
        viewModel.IsFileHistoryEnabled.Should().BeFalse();
        viewModel.IsFilterFileInGridVisible.Should().BeFalse();
        viewModel.IsDiffWithRememberedVisible.Should().BeFalse();
    }

    [Test]
    public void A_new_working_directory_file_should_offer_staging_ignoring_and_opening_with_on_windows_only()
    {
        DiffMenuSelection selection = new(ObjectId.IndexId, ObjectId.WorkTreeId, [],
            [new DiffMenuFile("?", "new.txt", null, IsTracked: false)]);
        DiffFileMenuState state = State(selection);
        FileListMenuViewModel windows = new(_preferences, isWindows: true, visualStudioInstalled: () => true);
        FileListMenuViewModel mac = new(_preferences, isWindows: false, visualStudioInstalled: () => true);

        windows.Refresh(state, null, new FileListMenuContext(), new FileListTreeState());
        mac.Refresh(state, null, new FileListMenuContext(), new FileListTreeState());

        windows.IsStageVisible.Should().Be(state.Stage).And.BeTrue();
        windows.IsAddToIgnoreVisible.Should().BeTrue();
        windows.IsOpenWorkingFileVisible.Should().BeTrue();
        windows.IsOpenWorkingFileWithVisible.Should().BeTrue();
        windows.IsOpenInVisualStudioVisible.Should().Be(state.EditWorkingFile);
        mac.IsOpenWorkingFileWithVisible.Should().BeFalse("the system's \"Open with\" dialog is Windows'");
        mac.IsOpenInVisualStudioVisible.Should().BeFalse();
    }

    [Test]
    public void The_items_a_window_did_not_give_should_hide_and_file_history_should_be_the_default_where_the_tree_is()
    {
        DiffFileMenuState state = State(new DiffMenuSelection(Parent, Commit, [Parent],
            [new DiffMenuFile("M", "a.txt", null, IsTracked: true)]));
        FileListMenuViewModel viewModel = new(_preferences, isWindows: false);

        viewModel.Refresh(state, null, new FileListMenuContext(), new FileListTreeState());

        viewModel.IsShowInFileTreeVisible.Should().BeFalse();
        viewModel.IsFilterFileInGridVisible.Should().BeFalse();
        viewModel.IsFileHistoryEnabled.Should().BeFalse("no window to show it was given");
        viewModel.IsBlameEnabled.Should().BeFalse();
        viewModel.IsFileHistoryDefault.Should().BeFalse();
        viewModel.IsCherryPickVisible.Should().BeFalse();

        viewModel.Refresh(state, null, new FileListMenuContext(HasServices: true, HasFileHistory: true,
            HasShowInFileTree: true, CanShowInFileTree: true, HasFilterFileInGrid: true, BlameChecked: true),
            new FileListTreeState());

        viewModel.IsShowInFileTreeVisible.Should().Be(state.ShowInFileTree);
        viewModel.IsFilterFileInGridVisible.Should().BeTrue();
        viewModel.IsFileHistoryEnabled.Should().Be(state.FileHistory);
        viewModel.IsBlameEnabled.Should().Be(state.Blame);
        viewModel.IsFileHistoryDefault.Should().BeTrue();
        viewModel.IsBlameCheckable.Should().BeTrue();
        viewModel.IsBlameChecked.Should().BeTrue();
        viewModel.ResetToFirstHeader.Should().Be(state.ResetToFirst);
        viewModel.DeleteFileHeader.Should().Be(state.DeleteFileText);
    }

    [Test]
    public void A_submodule_that_opens_in_its_own_window_should_be_the_default_unless_a_double_click_stages()
    {
        _preferences.OpenSubmoduleDiffInSeparateWindow = true;
        DiffFileMenuState state = State(new DiffMenuSelection(Parent, Commit, [Parent],
            [new DiffMenuFile("M", "sub", null, IsTracked: true) { IsSubmodule = true }]));
        FileListMenuViewModel viewModel = new(_preferences, isWindows: false);

        viewModel.Refresh(state, null, new FileListMenuContext(HasSubmodules: true, HasShowInFileTree: true),
            new FileListTreeState());

        viewModel.IsOpenSubmoduleVisible.Should().Be(state.OpenSubmodule).And.BeTrue();
        viewModel.IsOpenSubmoduleDefault.Should().BeTrue();
        viewModel.IsFileHistoryDefault.Should().BeFalse("the double-click opens the submodule");

        viewModel.Refresh(state, null, new FileListMenuContext(HasSubmodules: true, HasOwnStage: true),
            new FileListTreeState());

        viewModel.IsOpenSubmoduleDefault.Should().BeFalse("the commit window's double-click stages");
    }

    [Test]
    public void The_remembered_files_item_should_take_upstreams_text_with_its_access_key()
    {
        DiffFileMenuState state = State(new DiffMenuSelection(Parent, Commit, [Parent],
            [new DiffMenuFile("M", "a.txt", null, IsTracked: true)]));
        DiffRememberState remember = new(Separator: true, DiffTwoSelected: false, DiffTwoSelectedEnabled: false,
            DiffWithRemembered: "Diff with &remembered a.txt", DiffWithRememberedEnabled: true, RememberSecond: true,
            RememberSecondEnabled: true, RememberFirst: false, RememberFirstEnabled: false);
        FileListMenuViewModel viewModel = new(_preferences, isWindows: false);

        viewModel.Refresh(state, remember, new FileListMenuContext(), new FileListTreeState());

        viewModel.IsRememberSeparatorVisible.Should().BeTrue();
        viewModel.DiffWithRememberedHeader.Should().Be("Diff with _remembered a.txt");
        viewModel.IsDiffWithRememberedEnabled.Should().BeTrue();
        viewModel.IsRememberSecondVisible.Should().BeTrue();
        viewModel.IsRememberFirstVisible.Should().BeFalse();
    }

    private DiffFileMenuState State(DiffMenuSelection selection)
        => DiffFileMenu.For(selection, _workingDir, id => id.ToShortString());
}
