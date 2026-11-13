using AwesomeAssertions;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SmallDialogViewModelsTests
{
    [Test]
    public void ChooseViewModel_should_start_on_the_first_option_and_close_with_the_chosen_one()
    {
        FakeDialogHost host = new();
        ChooseViewModel viewModel = new(host, ["origin", "upstream"]);
        viewModel.Selected.Should().Be("origin");

        viewModel.Selected = "upstream";
        viewModel.SelectCommand.Execute(null);

        host.Result.Should().Be("upstream");
    }

    [Test]
    public void ChooseViewModel_cancel_should_close_with_null()
    {
        FakeDialogHost host = new();
        ChooseViewModel viewModel = new(host, ["origin"]);

        viewModel.CancelCommand.Execute(null);

        host.IsClosed.Should().BeTrue();
        host.Result.Should().BeNull();
    }

    [Test]
    public void MessageViewModel_and_TextViewModel_should_close_with_their_button()
    {
        FakeDialogHost messageHost = new();
        FakeDialogHost textHost = new();

        new MessageViewModel(messageHost, "failed").CloseCommand.Execute(null);
        new TextViewModel(textHost, "Help", "text").CloseCommand.Execute(null);

        messageHost.IsClosed.Should().BeTrue();
        textHost.IsClosed.Should().BeTrue();
    }

    [Test]
    public void ContributorsViewModel_should_translate_upstreams_texts_and_join_the_names()
    {
        ContributorsViewModel viewModel = new(new FakeDialogHost(), "Ann\nBob", "Cid", "Dee\r\nEve", "Fay");

        viewModel.Title.Should().Be(FakeDialogHost.TranslatedMark + Contributors.Caption);
        viewModel.DevelopersHeader.Should().Be(FakeDialogHost.TranslatedMark + Contributors.Developers);
        viewModel.DevelopersText.Should().StartWith(FakeDialogHost.TranslatedMark + Contributors.Team + ":\r\n");
        viewModel.TranslatorsText.Should().Be(Contributors.OneLine("Dee\r\nEve"));
        viewModel.DesignersText.Should().Be("Fay");
    }
}
