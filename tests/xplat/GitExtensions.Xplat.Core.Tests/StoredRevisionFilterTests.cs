using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's FilterInfo keeps some filter options in settings, which every revision grid starts from.
internal sealed class StoredRevisionFilterTests
{
    [Test]
    public void Initial_should_start_from_the_stored_options()
    {
        InMemoryAppPreferences preferences = new()
        {
            ShowCurrentBranchOnly = true,
            HideMergeCommits = true,
            ShowOnlyFirstParent = true,
            ShowSimplifyByDecoration = true,
            FileHistory = new FileHistoryOptions(FullHistory: true, SimplifyMerges: true),
        };

        StoredRevisionFilter.Initial(preferences).Should().Be(new RevisionFilter(CurrentBranchOnly: true, NoMerges: true,
            FirstParent: true, SimplifyByDecoration: true, FullHistory: true, SimplifyMerges: true));
    }

    [Test]
    public void Store_should_keep_the_options_upstream_stores_on_each_change()
    {
        InMemoryAppPreferences preferences = new();

        StoredRevisionFilter.Store(new RevisionFilter(CurrentBranchOnly: true, NoMerges: true, FirstParent: true,
            SimplifyByDecoration: true, FullHistory: true), preferences);

        preferences.HideMergeCommits.Should().BeTrue();
        preferences.ShowOnlyFirstParent.Should().BeTrue();
        preferences.ShowSimplifyByDecoration.Should().BeTrue();
        preferences.ShowCurrentBranchOnly.Should().BeFalse("only the branch choice and the filter window store it");
        preferences.FileHistory.FullHistory.Should().BeFalse("only the filter window and the file history store it");
    }

    [Test]
    public void StoreDialogChoice_should_keep_the_filter_windows_choices()
    {
        InMemoryAppPreferences preferences = new();

        StoredRevisionFilter.StoreDialogChoice(new RevisionFilter(CurrentBranchOnly: true, FullHistory: true,
            SimplifyMerges: true), preferences);

        preferences.ShowCurrentBranchOnly.Should().BeTrue();
        preferences.FileHistory.Should().Be(new FileHistoryOptions(FullHistory: true, SimplifyMerges: true));
    }
}
