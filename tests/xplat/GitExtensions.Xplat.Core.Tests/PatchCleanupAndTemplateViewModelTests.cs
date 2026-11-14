using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using NSubstitute;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PatchCleanupAndTemplateViewModelTests
{
    private const string WorkingDir = "/work/repo/";

    [Test]
    public async Task FormatPatchViewModel_should_need_a_folder_and_commits_then_make_the_patches_and_close()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.FormatPatch(Arg.Any<string>(), Arg.Any<string>(), "/out", Arg.Any<int?>()).Returns("0001-second.patch");
        FakeDialogHost host = new();
        FormatPatchViewModel viewModel = new(host, module, new InMemoryAppPreferences { LastFormatPatchDir = "" },
            new FakeCommitHistory());

        await viewModel.FormatPatchCommand.ExecuteAsync(null);
        viewModel.OutputPath = "/out";
        await viewModel.FormatPatchCommand.ExecuteAsync(null);
        viewModel.SelectedRows = [new CommitRow("abc", "abc", "second", "author", "today", ParentHashes: ["def"])];
        await viewModel.FormatPatchCommand.ExecuteAsync(null);

        host.Messages.Should().Equal(
            FakeDialogHost.TranslatedMark + FormatPatchDialog.NoOutputPathEntered,
            FakeDialogHost.TranslatedMark + FormatPatchDialog.RevisionsNeeded,
            "0001-second.patch");
        module.Received(1).FormatPatch(Arg.Any<string>(), Arg.Any<string>(), "/out", Arg.Any<int?>());
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public void FormatPatchViewModel_should_keep_only_an_existing_output_folder()
    {
        InMemoryAppPreferences preferences = new() { LastFormatPatchDir = "" };
        FormatPatchViewModel viewModel = new(new FakeDialogHost(), Substitute.For<IGitModule>(), preferences,
            new FakeCommitHistory());

        viewModel.OutputPath = "/no/such/folder";
        preferences.LastFormatPatchDir.Should().BeEmpty();
        viewModel.OutputPath = Path.GetTempPath();
        preferences.LastFormatPatchDir.Should().Be(Path.GetTempPath());
    }

    [Test]
    public async Task CleanupRepositoryViewModel_should_run_nothing_when_the_question_is_answered_No()
    {
        int cleaned = 0;
        FakeDialogHost host = new() { Answer = false };
        CleanupRepositoryViewModel viewModel = new(host, Substitute.For<IGitModule>(), () => cleaned++);

        await viewModel.CleanupCommand.ExecuteAsync(null);

        host.Questions.Should().Equal(FakeDialogHost.TranslatedMark + CleanupDialog.ReallyCleanupQuestion);
        cleaned.Should().Be(0);
        viewModel.Output.Should().BeEmpty();
    }

    [Test]
    public async Task CleanupRepositoryViewModel_should_add_a_picked_file_of_the_work_tree_and_turn_the_filter_on()
    {
        IGitModule module = Substitute.For<IGitModule>();
        module.WorkingDir.Returns(WorkingDir);
        CleanupRepositoryViewModel viewModel = new(new FakeDialogHost(), module, null)
        {
            PickFile = start => Task.FromResult<string?>(start + "keep.txt"),
        };

        await viewModel.AddExcludePathCommand.ExecuteAsync(null);
        viewModel.PickFile = _ => Task.FromResult<string?>("/elsewhere/other.txt");
        await viewModel.AddExcludePathCommand.ExecuteAsync(null);

        viewModel.ExcludePathFilter.Should().BeTrue();
        viewModel.ExcludePaths.Should().Be("keep.txt", "a file outside the work tree is not added");
        viewModel.IncludePathFilter.Should().BeFalse();
    }

    [Test]
    public void CommitTemplateSettingsViewModel_should_rename_the_selected_template_and_store_everything_on_OK()
    {
        InMemoryAppPreferences preferences = new() { CommitTemplates = "", CommitValidation = new CommitValidationOptions() };
        FakeDialogHost host = new();
        CommitTemplateSettingsViewModel viewModel = new(host, preferences);
        viewModel.TemplateNames.Should().HaveCount(10);
        viewModel.TemplateNames[0].Should().Be("1 : <" + FakeDialogHost.TranslatedMark + "empty>");

        viewModel.TemplateName = "Fix";
        viewModel.TemplateText = "fix: ";
        viewModel.SelectedIndex = 1;
        viewModel.TemplateName.Should().BeEmpty("the second template is empty");
        viewModel.MaxFirstLineLength = 5;
        viewModel.OkCommand.Execute(null);

        viewModel.TemplateNames[0].Should().Be("1 : Fix");
        preferences.CommitValidation.MaxFirstLineLength.Should().Be(5);
        CommitTemplates.FromSettings(preferences.CommitTemplates).Single().Should()
            .Be(new CommitTemplate("Fix", "fix: ", IsRegex: false));
        host.Result.Should().Be(true);
    }

    [Test]
    public void FindInCommitFilesGitGrepViewModel_should_store_settings_only_once_loaded_and_clear_the_search_on_close()
    {
        InMemoryAppPreferences preferences = new() { GitGrepIgnoreCase = true, GitGrepMatchWholeWord = false };
        List<string> located = [];
        List<bool> toggled = [];
        FindInCommitFilesGitGrepViewModel viewModel = new(new FakeDialogHost(), preferences, located.Add, toggled.Add);

        viewModel.MatchWholeWord = true;
        preferences.GitGrepMatchWholeWord.Should().BeFalse("nothing is written before the window read the settings");
        viewModel.Load();
        viewModel.MatchCase = true;
        viewModel.SetShowSearchBox(true);
        viewModel.Expression = "needle";
        viewModel.FindCommand.Execute(null);
        viewModel.OnClosing();
        viewModel.ShowSearchBox = false;
        viewModel.OnClosing();

        preferences.GitGrepIgnoreCase.Should().BeFalse();
        toggled.Should().Equal([false], "following the list's box toggles nothing");
        located.Should().Equal("needle", "");
    }
}
