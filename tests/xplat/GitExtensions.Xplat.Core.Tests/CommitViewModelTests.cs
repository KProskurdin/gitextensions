using AwesomeAssertions;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitUI.ScriptsEngine;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// The lists' trees, the diff and the spelling check are the window's; these cover the message, amend, the commit, what the
// lists list and the menus.
internal sealed class CommitViewModelTests
{
    private const string RepositoryPath = "repo";

    private readonly List<Confirmation> _confirmations = [];
    private readonly List<ScriptEvent> _scripts = [];
    private FakeGitOperations _operations = null!;
    private ICommitMessageStore _messages = null!;
    private IRepositoryService _service = null!;
    private InMemoryAppPreferences _preferences = null!;
    private bool _beforeCommitPasses;
    private bool _confirms;

    [SetUp]
    public void SetUp()
    {
        _confirmations.Clear();
        _scripts.Clear();
        _operations = new FakeGitOperations();
        _messages = Substitute.For<ICommitMessageStore>();
        _service = Substitute.For<IRepositoryService>();
        _preferences = new InMemoryAppPreferences();
        _beforeCommitPasses = true;
        _confirms = true;
    }

    [Test]
    public async Task LoadAsync_should_start_from_the_draft()
    {
        Task<string> draft = Task.FromResult("draft\n");
        _messages.LoadAsync(RepositoryPath).Returns(draft);
        CommitViewModel viewModel = New(new FakeDialogHost());

        await viewModel.LoadAsync();

        viewModel.Message.Should().Be("draft");
        viewModel.Amend.Should().BeFalse();
    }

    [Test]
    public void Amend_should_show_the_head_message_and_unchecking_it_should_put_back_the_typed_one()
    {
        Task<string> head = Task.FromResult("head message");
        _service.GetHeadMessageAsync(RepositoryPath).Returns(head);
        CommitViewModel viewModel = New(new FakeDialogHost());
        viewModel.Message = "typed";

        viewModel.Amend = true;

        viewModel.Message.Should().Be("head message");
        viewModel.CommitAndPushContent.Should().Be(CommitViewModel.CommitAndPushText);

        viewModel.Amend = false;

        viewModel.Message.Should().Be("typed");
    }

    [Test]
    public async Task Commit_should_commit_the_message_reset_the_draft_and_close()
    {
        FakeDialogHost host = new();
        CommitViewModel viewModel = New(host);
        viewModel.Message = "subject";
        viewModel.SignOff = true;

        Task commit = viewModel.CommitCommand.ExecuteAsync(null);
        _operations.Complete(0);
        await commit;

        _confirmations.Should().Equal(Confirmation.CommitWithoutBranch);
        _operations.NameAt(0).Should().Be("Commit");
        _operations.ArgumentsAt(0).Should().StartWith($"{RepositoryPath} subject")
            .And.Contain("amend=False signOff=True");
        _scripts.Should().Equal(ScriptEvent.BeforeCommit, ScriptEvent.AfterCommit);
        await _messages.Received(1).ResetAsync(RepositoryPath);
        viewModel.Message.Should().BeEmpty();
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task A_before_commit_script_should_stop_the_commit()
    {
        _beforeCommitPasses = false;
        FakeDialogHost host = new();
        CommitViewModel viewModel = New(host);
        viewModel.Message = "subject";

        await viewModel.CommitCommand.ExecuteAsync(null);

        _operations.Count.Should().Be(0);
        viewModel.Message.Should().Be("subject");
        host.IsClosed.Should().BeFalse();
    }

    [Test]
    public void A_fixup_message_should_be_read_only_until_modified()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());

        viewModel.StartSpecialCommit(CommitKind.Fixup, "subject", "subject\n\nbody");

        viewModel.Message.Should().StartWith("fixup! subject");
        (viewModel.CanChangeMessage, viewModel.IsModifyMessageVisible, viewModel.KeepsDraft).Should()
            .Be((false, true, false));

        int focused = 0;
        viewModel.MessageFocusRequested += (_, _) => focused++;

        viewModel.ModifyMessageCommand.Execute(null);

        (viewModel.CanChangeMessage, viewModel.IsModifyMessageVisible).Should().Be((true, false));
        focused.Should().Be(1, "the message is next, as upstream focuses it");
    }

    [TestCase(true, 1)]
    [TestCase(false, 0)]
    public async Task ResetSoft_should_focus_the_message_only_after_the_reset(bool confirms, int focused)
    {
        CommitViewModel viewModel = New(new FakeDialogHost());
        int requests = 0;
        viewModel.MessageFocusRequested += (_, _) => requests++;
        _confirms = confirms;

        Task reset = viewModel.ResetSoftCommand.ExecuteAsync(null);
        if (confirms)
        {
            _operations.ArgumentsAt(0).Should().Be("repo parent Soft");
            _operations.Complete(0);
        }

        await reset;

        requests.Should().Be(focused);
        _operations.Count.Should().Be(confirms ? 1 : 0);
    }

    [Test]
    public void A_specific_GPG_key_should_show_its_box_and_the_key_on_the_button()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());

        viewModel.GpgSignIndex = CommitViewModel.GpgSpecificKey;

        viewModel.IsGpgKeyVisible.Should().BeTrue();
        viewModel.CommitIcon.Should().Be("Key");
    }

    [Test]
    public async Task StageAllFiles_should_stage_only_the_files_a_filtered_list_shows_and_clear_its_filter()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());
        List<bool> cleared = [];
        viewModel.FilterClearRequested += (_, staged) => cleared.Add(staged);
        viewModel.StageAllImage.Should().Be("StageAll");

        viewModel.SetShown(staged: false, [new FileChange("notes.md", ChangeKind.Untracked, Staged: false)], filtered: true);

        (viewModel.StageAllImage, viewModel.StageAllTip).Should()
            .Be(("StageAllFiltered", FakeDialogHost.TranslatedMark + CommitViewModel.StageFilteredText));

        Task stage = viewModel.StageAllFilesCommand.ExecuteAsync(false);
        _operations.Complete(0);
        await stage;

        _operations.NameAt(0).Should().Be("Stage");
        _operations.ArgumentsAt(0).Should().Be("repo notes.md");
        cleared.Should().Equal(false);
    }

    [Test]
    public async Task The_list_settings_should_list_the_working_directory_read_with_the_flags_checked()
    {
        List<(bool Ignored, bool AssumeUnchanged, bool SkipWorktree)> reads = [];
        IReadOnlyList<FileChange> flagged = [new FileChange("build.log", ChangeKind.Untracked, Staged: false)];
        CommitViewModel viewModel = New(new FakeDialogHost(), services => services with
        {
            ReadWorkTreeChanges = (ignored, assumeUnchanged, skipWorktree) =>
            {
                reads.Add((ignored, assumeUnchanged, skipWorktree));
                return Task.FromResult(flagged);
            },
        });
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.ToggleListSettingCommand.Execute(CommitListSetting.ShowIgnored);
        await Task.Yield();

        reads.Should().Equal((true, false, false));
        viewModel.ListedChanges.Should().BeSameAs(flagged);
        raised.Should().Contain(nameof(CommitViewModel.ListedChanges));

        viewModel.ToggleListSettingCommand.Execute(CommitListSetting.ShowIgnored);

        viewModel.ListedChanges.Should().BeEmpty("the repository's changes are listed again");
        reads.Should().HaveCount(1);

        viewModel.ToggleListSettingCommand.Execute(CommitListSetting.ShowUntracked);

        viewModel.ShowUntracked.Should().BeFalse();
        reads.Should().HaveCount(1, "untracked files are only hidden");
    }

    [Test]
    public async Task LoadAsync_should_read_show_untracked_files_and_the_dictionary_as_the_repository_has_them()
    {
        List<string> keys = [];
        _messages.LoadAsync(RepositoryPath).Returns(Task.FromResult(""));
        CommitViewModel viewModel = New(new FakeDialogHost(), services => services with
        {
            ReadGitSetting = key =>
            {
                keys.Add(key);
                return Task.FromResult("no");
            },
            LoadDictionary = () => Task.FromResult("de-DE"),
        });
        viewModel.Message = "typed";

        await viewModel.LoadAsync();

        keys.Should().Equal("status.showuntrackedfiles");
        viewModel.ShowUntracked.Should().BeFalse();
        viewModel.SpellingDictionary.Should().Be("de-DE");
    }

    [Test]
    public void The_templates_menu_should_list_the_templates_then_conventional_commits_and_the_settings()
    {
        _preferences.CommitTemplates = CommitTemplates.Serialize([new CommitTemplate("Mine", "my text", false)]);
        CommitViewModel viewModel = New(new FakeDialogHost());

        IReadOnlyList<MenuEntry> entries = viewModel.TemplateMenuEntries();

        entries.Select(entry => entry.IsSeparator ? null : entry.Text).Should().Equal("Mine", null,
            FakeDialogHost.TranslatedMark + CommitViewModel.ConventionalCommitText, null,
            FakeDialogHost.TranslatedMark + CommitViewModel.TemplateSettingsText);
        entries[2].Name.Should().Be(CommitViewModel.ConventionalCommitsName);
        entries[2].Children.Select(entry => entry.Text).Should().Contain(["feat", "fix", "BREAKING CHANGE", "[skip ci]"]);

        int focused = 0;
        viewModel.MessageFocusRequested += (_, _) => focused++;
        entries[0].Command!.Execute(entries[0].CommandParameter);

        viewModel.Message.Should().Be("my text");
        focused.Should().Be(1);
    }

    [Test]
    public void A_conventional_commit_type_should_go_before_the_subject_and_put_the_caret_after_it()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());
        viewModel.Message = "my text";
        viewModel.MessageCaret = 3;
        List<int> carets = [];
        viewModel.CaretRequested += (_, caret) => carets.Add(caret);

        viewModel.ApplyConventionalTypeCommand.Execute("fix");

        viewModel.Message.Should().Be("fix: my text");
        carets.Should().ContainSingle();

        viewModel.AppendFooterCommand.Execute("[skip ci]");

        viewModel.Message.Should().EndWith(Environment.NewLine + "[skip ci]");
        carets[1].Should().Be(3, "upstream's skip ci footer leaves the caret where it is");
    }

    [TestCase(0, 0, "picked\nmy text", 7)]
    [TestCase(7, 3, "my picked", 9)]
    public void Add_selection_to_commit_message_should_replace_the_messages_selection_or_go_in_on_a_line_of_its_own(
        int start, int end, string expected, int caret)
    {
        CommitViewModel viewModel = New(new FakeDialogHost());
        viewModel.Message = "my text";
        viewModel.MessageSelection = (start, end);
        DiffViewModel diff = new(Substitute.For<IDiffService>());
        diff.SetSelection(-1, [], "picked");
        List<int> carets = [];
        viewModel.MessageCaretMoved += (_, moved) => carets.Add(moved);

        viewModel.AddSelectionToCommitMessageCommand.Execute(diff);

        viewModel.Message.Should().Be(expected);
        carets.Should().Equal(caret);
    }

    [Test]
    public void Add_selection_to_commit_message_should_leave_the_message_when_nothing_is_selected_in_the_diff()
    {
        CommitViewModel viewModel = New(new FakeDialogHost());
        viewModel.Message = "my text";
        viewModel.MessageSelection = (0, 7);

        viewModel.TryAddSelectionToCommitMessage("").Should().BeFalse();

        viewModel.Message.Should().Be("my text");
    }

    [Test]
    public async Task A_dictionary_chosen_in_the_menu_should_be_checked_and_kept_for_the_repository()
    {
        List<string> saved = [];
        CommitViewModel viewModel = New(new FakeDialogHost(), services => services with
        {
            SaveDictionary = dictionary =>
            {
                saved.Add(dictionary);
                return Task.CompletedTask;
            },
        });

        MenuEntry menu = viewModel.DictionaryEntry(["None", "de-DE", "en-US"]);
        menu.Children.Single(entry => entry.IsChecked == true).Text.Should().Be("None");
        await ((IAsyncRelayCommand<string>)menu.Children[1].Command!).ExecuteAsync("de-DE");

        viewModel.SpellingDictionary.Should().Be("de-DE");
        saved.Should().Equal("de-DE");
        viewModel.DictionaryEntry(["None", "de-DE"]).Children.Single(entry => entry.IsChecked == true).Text.Should()
            .Be("de-DE");
    }

    [Test]
    public void The_message_box_settings_should_turn_their_preferences_on_and_off()
    {
        _preferences.MarkIllFormedLinesInCommitMsg = true;
        _preferences.ProvideAutocompletion = true;
        CommitViewModel viewModel = New(new FakeDialogHost());
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        IReadOnlyList<MenuEntry> entries = viewModel.MessageBoxSettingEntries();
        entries.Select(entry => entry.IsChecked).Should().Equal(true, true);

        entries[0].Command!.Execute(null);
        entries[1].Command!.Execute(null);

        (_preferences.MarkIllFormedLinesInCommitMsg, _preferences.ProvideAutocompletion).Should().Be((false, false));
        raised.Should().Equal(nameof(CommitViewModel.MarkIllFormedLines), nameof(CommitViewModel.ProvideAutoCompletion));
    }

    private CommitViewModel New(FakeDialogHost host, Func<CommitViewModelServices, CommitViewModelServices>? with = null)
    {
        CommitViewModelServices services = new()
        {
            ConfirmSuppressible = (confirmation, _, _, _) =>
            {
                _confirmations.Add(confirmation);
                return Task.FromResult(_confirms);
            },
            AskValidation = (_, _) => Task.FromResult(true),
            RunScripts = scriptEvent =>
            {
                _scripts.Add(scriptEvent);
                return Task.FromResult(scriptEvent != ScriptEvent.BeforeCommit || _beforeCommitPasses);
            },
            Push = _ => Task.FromResult(true),
            AskResetChanges = (_, _) => Task.FromResult(ResetChangesAction.Cancel),
            CreateBranch = () => Task.CompletedTask,
            ShowError = (_, _) => { },
            ResolveCommit = _ => Task.FromResult<string?>("parent"),
        };
        return new CommitViewModel(host, RepositoryPath, new RepositoryViewModel(new FakeRepositoryService()),
            new RepositoryOperationsViewModel(_operations), _service, _preferences, () => Task.CompletedTask, _messages,
            with is null ? services : with(services));
    }
}
