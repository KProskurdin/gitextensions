using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SmallDialogViewModels3Tests
{
    [Test]
    public void AskPassViewModel_should_hide_a_password_and_answer_only_on_OK()
    {
        FakeDialogHost host = new();
        AskPassViewModel password = new(host, "Password for 'https://example.com': ");
        AskPassViewModel user = new(new FakeDialogHost(), "Username for 'https://example.com': ");

        password.IsSecret.Should().BeTrue();
        user.IsSecret.Should().BeFalse();
        password.Prompt.Should().Be("Password for 'https://example.com':");
        password.Text = "s3cret";
        password.CancelCommand.Execute(null);
        password.Answer.Should().BeNull("cancel answers nothing");

        password.OkCommand.Execute(null);
        password.Answer.Should().Be("s3cret");
        host.IsClosed.Should().BeTrue();
    }

    [TestCase(true, true, true, false)]
    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, true)]
    public void ResetChangesViewModel_should_offer_deleting_new_files_as_upstream(bool existing, bool newFiles,
        bool canChoose, bool deletes)
    {
        ResetChangesViewModel viewModel = new(new FakeDialogHost(), existing, newFiles);

        viewModel.CanChooseDeleteNewFiles.Should().Be(canChoose);
        viewModel.DeleteNewFiles.Should().Be(deletes);
    }

    [Test]
    public void ResetChangesViewModel_should_close_with_the_action()
    {
        FakeDialogHost host = new();
        ResetChangesViewModel viewModel = new(host, true, true, "Reset these?") { DeleteNewFiles = true };

        viewModel.ResetCommand.Execute(null);

        viewModel.Message.Should().Be("Reset these?");
        host.Result.Should().Be(ResetChangesAction.ResetAndDelete);
    }

    [Test]
    public async Task DeleteTagViewModel_should_delete_on_the_remote_only_when_asked()
    {
        List<(string Tag, string? Remote)> deleted = [];
        DeleteTagViewModel viewModel = new(new FakeDialogHost(), "v1", ["v1", "v2"], ["origin", "fork"], "fork",
            (tag, remote) =>
            {
                deleted.Add((tag, remote));
                return Task.CompletedTask;
            });
        viewModel.SelectedRemote.Should().Be("fork", "the branch's remote is chosen first");

        await viewModel.DeleteCommand.ExecuteAsync(null);
        viewModel.DeleteFromRemote = true;
        await viewModel.DeleteCommand.ExecuteAsync(null);

        deleted.Should().Equal(("v1", null), ("v1", "fork"));
    }

    [Test]
    public void ConfirmViewModel_should_close_with_the_answer_and_keep_the_check_box()
    {
        FakeDialogHost host = new();
        ConfirmViewModel viewModel = new(host, "Delete?", "Delete", offerDontShowAgain: true) { DontShowAgain = true };

        viewModel.ConfirmCommand.Execute(null);

        host.Result.Should().Be(true);
        viewModel.DontShowAgain.Should().BeTrue();
        viewModel.Caption.Should().Be("Git Extensions");
        viewModel.DontShowAgainText.Should().Be(Confirmations.DontShowAgain);
    }
}
