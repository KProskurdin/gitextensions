using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class ProcessAndAboutViewModelTests
{
    private const string RepositoryPath = "/work/repo";

    [Test]
    public async Task ProcessViewModel_should_follow_the_operation_and_close_after_success_unless_kept_open()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel actions = new(git);
        FakeDialogHost host = new();
        ProcessViewModel viewModel = new(host, actions, new InMemoryAppPreferences { CloseProcessDialog = false });

        Task fetch = actions.FetchAsync(RepositoryPath, "origin", prune: false);

        viewModel.StateText.Should().Be("Running...");
        viewModel.AbortCommand.CanExecute(null).Should().BeTrue();
        viewModel.CloseCommand.CanExecute(null).Should().BeFalse();
        viewModel.Title.Should().Be(actions.OutputTitle);
        git.Complete(0);
        await fetch;
        viewModel.StateText.Should().Be("Done");
        viewModel.CloseCommand.CanExecute(null).Should().BeTrue();
        host.IsClosed.Should().BeFalse("the dialog is kept open");

        viewModel.KeepDialogOpen = false;

        host.IsClosed.Should().BeTrue("turning the setting off after success closes the dialog");
        viewModel.Detach();
    }

    [Test]
    public async Task ProcessViewModel_should_close_by_itself_after_success_when_not_kept_open()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel actions = new(git);
        FakeDialogHost host = new();
        InMemoryAppPreferences preferences = new() { CloseProcessDialog = true };
        ProcessViewModel viewModel = new(host, actions, preferences);
        viewModel.KeepDialogOpen.Should().BeFalse();

        Task fetch = actions.FetchAsync(RepositoryPath, "origin", prune: false);
        git.Complete(0);
        await fetch;

        host.IsClosed.Should().BeTrue();
        viewModel.Detach();
    }

    [Test]
    public async Task ProcessViewModel_should_show_a_failure()
    {
        FakeGitOperations git = new();
        RepositoryOperationsViewModel actions = new(git);
        ProcessViewModel viewModel = new(new FakeDialogHost(), actions, new InMemoryAppPreferences());

        Task fetch = actions.FetchAsync(RepositoryPath, "origin", prune: false);
        git.Fail(0, new InvalidOperationException("no remote"));
        await fetch;

        viewModel.IsFailed.Should().BeTrue();
        viewModel.StateText.Should().StartWith("Failed");
        viewModel.Detach();
    }

    [Test]
    public void AboutViewModel_should_thank_a_contributor_and_show_the_information_without_marks()
    {
        AboutViewModel viewModel = new(new FakeDialogHost(), "Git Extensions", [" ann ", "bob"], new Random(1));

        viewModel.ThanksText.Should().StartWith(FakeDialogHost.TranslatedMark + "Thanks to over 2 contributors: ")
            .And.Match(text => text.EndsWith("ann") || text.EndsWith("bob"));
        viewModel.ShowInformation("- Git Extensions 6.0\r\n- Git 2.53.0");
        viewModel.EnvironmentInfo.Should().Be("Git Extensions 6.0\r\nGit 2.53.0");
        viewModel.ProductName.Should().Be("Git Extensions");
    }
}
