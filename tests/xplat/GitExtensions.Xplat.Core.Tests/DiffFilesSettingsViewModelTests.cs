using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class DiffFilesSettingsViewModelTests
{
    private readonly List<string> _steps = [];
    private InMemoryAppPreferences _preferences = null!;

    [SetUp]
    public void SetUp()
    {
        _steps.Clear();
        _preferences = new InMemoryAppPreferences();
    }

    [Test]
    public void The_show_items_should_show_the_files_again_and_apply_only_to_the_working_directory()
    {
        DiffFilesSettingsViewModel settings = New();
        settings.Refresh(workTreeSelected: false);
        settings.IsWorkTreeSelected.Should().BeFalse();

        settings.Refresh(workTreeSelected: true);
        settings.ShowSkipWorktree = true;
        settings.ShowUntracked = false;

        settings.IsWorkTreeSelected.Should().BeTrue();
        _steps.Should().Equal("refresh", "refresh");
        _preferences.SaveCount.Should().Be(0, "the show items are the window's own");
    }

    [Test]
    public async Task Show_untracked_files_should_start_as_git_says_without_calculating_the_groups()
    {
        DiffFilesSettingsViewModel settings = New(gitValue: "no");
        settings.ShowUntracked.Should().BeTrue();

        await settings.ReadShowUntrackedAsync("/work/repo");

        settings.ShowUntracked.Should().BeFalse();
        _steps.Should().Equal("read /work/repo status.showuntrackedfiles", "show");
    }

    [Test]
    public void The_stored_choices_should_be_saved_at_once_and_all_parents_should_calculate_the_groups()
    {
        _preferences.RefreshCommitDialogOnFocus = false;
        _preferences.ShowDiffForAllParents = false;
        DiffFilesSettingsViewModel settings = New();

        settings.RefreshOnFormFocus = true;
        settings.ShowDiffForAllParents = true;

        (_preferences.RefreshCommitDialogOnFocus, _preferences.ShowDiffForAllParents).Should().Be((true, true));
        _preferences.SaveCount.Should().Be(2);
        _steps.Should().Equal("groups");
    }

    [Test]
    public void Opening_the_menu_should_show_the_choices_the_settings_window_changed()
    {
        DiffFilesSettingsViewModel settings = New();
        List<string?> changed = [];
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _preferences.ShowDiffForAllParents = !_preferences.ShowDiffForAllParents;

        settings.Refresh(workTreeSelected: false);

        settings.ShowDiffForAllParents.Should().Be(_preferences.ShowDiffForAllParents);
        changed.Should().Contain([nameof(settings.RefreshOnFormFocus), nameof(settings.ShowDiffForAllParents)]);
        _steps.Should().BeEmpty();
    }

    private DiffFilesSettingsViewModel New(string gitValue = "")
        => new(_preferences, new DiffFilesSettingsServices
        {
            RefreshFiles = () => _steps.Add("refresh"),
            ShowFiles = () => _steps.Add("show"),
            RecalculateGroups = () => _steps.Add("groups"),
            GitSetting = (path, key) =>
            {
                lock (_steps)
                {
                    _steps.Add($"read {path} {key}");
                }

                return gitValue;
            },
        });
}
