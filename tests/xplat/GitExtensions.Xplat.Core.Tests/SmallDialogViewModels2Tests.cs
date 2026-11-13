using System.Text;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Platform;
using GitExtensions.Xplat.Core.Presentation;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class SmallDialogViewModels2Tests
{
    [Test]
    public void DonateViewModel_should_translate_upstreams_text_and_open_the_donation_page()
    {
        List<string> opened = [];
        DonateViewModel viewModel = new(new FakeDialogHost(), opened.Add);

        viewModel.DonateCommand.Execute(null);

        viewModel.Text.Should().Be(FakeDialogHost.TranslatedMark + DonateViewModel.DonateText);
        opened.Should().Equal(DonateViewModel.DonationUrl);
    }

    [Test]
    public void SelectMultipleBranchesViewModel_should_check_the_given_branches_and_report_the_checked_ones_in_order()
    {
        SelectMultipleBranchesViewModel viewModel = new(new FakeDialogHost(), ["main", "merged", "other"], ["merged"]);

        viewModel.Branches.Single(branch => branch.Name == "other").IsSelected = true;

        viewModel.SelectedBranches.Should().Equal("merged", "other");
    }

    [Test]
    public void GoToLineViewModel_should_name_the_range_and_close_with_the_line()
    {
        FakeDialogHost host = new();
        GoToLineViewModel viewModel = new(host);

        viewModel.SetMaxLineNumber(5);
        viewModel.Line = 3;
        viewModel.OkCommand.Execute(null);

        viewModel.Label.Should().Be(FakeDialogHost.TranslatedMark + "Line number (1 - 5):");
        viewModel.Maximum.Should().Be(5);
        host.Result.Should().Be(3);
    }

    [Test]
    public void PromptViewModel_should_close_with_the_trimmed_values_or_null_when_empty()
    {
        FakeDialogHost host = new();
        PromptViewModel viewModel = new(host, "Tag", "Name", "OK", secondPlaceholder: "Message") { Value = " v1 " };
        viewModel.SecondValue = " first ";
        viewModel.HasSecondValue.Should().BeTrue();

        viewModel.AcceptCommand.Execute(null);
        host.Result.Should().Be(new PromptResult("v1", "first"));

        FakeDialogHost emptyHost = new();
        new PromptViewModel(emptyHost, "Tag", "Name", "OK").AcceptCommand.Execute(null);
        emptyHost.IsClosed.Should().BeTrue();
        emptyHost.Result.Should().BeNull("an empty name is refused unless allowed");
    }

    [Test]
    public async Task ChangeLogViewModel_should_read_the_change_log()
    {
        ChangeLogViewModel viewModel =
            new(new FakeDialogHost(), () => new MemoryStream(Encoding.UTF8.GetBytes("## 6.0")));

        await viewModel.LoadAsync();

        viewModel.Text.Should().Be("## 6.0");
        viewModel.Loaded.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task AddFilesViewModel_should_dry_run_then_add_and_close_with_true()
    {
        FakeDialogHost host = new();
        List<string> ran = [];
        AddFilesViewModel viewModel = new(host, "new.txt", arguments =>
        {
            ran.Add(arguments);
            return Task.FromResult(true);
        });
        viewModel.Force = true;

        await viewModel.ShowFilesCommand.ExecuteAsync(null);
        host.IsClosed.Should().BeFalse("a dry run only shows the files");
        await viewModel.AddFilesCommand.ExecuteAsync(null);

        ran.Should().Equal(ApplyPatchDialog.AddFilesArguments("new.txt", force: true, dryRun: true),
            ApplyPatchDialog.AddFilesArguments("new.txt", force: true, dryRun: false));
        host.Result.Should().Be(true);
    }
}
