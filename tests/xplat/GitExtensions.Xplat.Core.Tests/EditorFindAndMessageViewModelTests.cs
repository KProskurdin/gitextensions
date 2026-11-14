using System.Text;
using AwesomeAssertions;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Editing;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Presentation;
using NSubstitute;
using NUnit.Framework;
using DialogResult = System.Windows.Forms.DialogResult;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using MessageBoxDefaultButton = System.Windows.Forms.MessageBoxDefaultButton;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class EditorFindAndMessageViewModelTests
{
    private string _folder = null!;

    [SetUp]
    public void SetUp()
        => _folder = Directory
            .CreateDirectory(Path.Combine(Path.GetTempPath(), "xplat-vm-" + Guid.NewGuid().ToString("N")))
            .FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void MessageBoxViewModel_should_offer_WinForms_buttons_and_close_with_the_one_pressed()
    {
        FakeDialogHost host = new();
        MessageBoxViewModel viewModel = new(host, "Sure?", "", MessageBoxButtons.YesNoCancel,
            MessageBoxDefaultButton.Button2);

        viewModel.Title.Should().Be("Git Extensions", "an empty caption shows the app's name");
        viewModel.Choices.Select(choice => choice.Name).Should().Equal("YesButton", "NoButton", "CancelButton");
        viewModel.Choices.Single(choice => choice.IsDefault).Result.Should().Be(DialogResult.No);
        viewModel.Choices.Single(choice => choice.IsCancel).Result.Should().Be(DialogResult.Cancel);
        viewModel.Result.Should().Be(DialogResult.Cancel, "closing a box with Cancel is Cancel");

        viewModel.ChooseCommand.Execute(viewModel.Choices[0]);

        viewModel.Result.Should().Be(DialogResult.Yes);
        host.Result.Should().Be(DialogResult.Yes);
    }

    [Test]
    public void FindViewModel_should_title_with_the_file_and_selection_and_raise_the_searches()
    {
        FindViewModel viewModel = new(new FakeDialogHost());
        List<bool> searches = [];
        int replaced = 0;
        viewModel.FindNextRequested += (_, backward) => searches.Add(backward);
        viewModel.ReplaceRequested += (_, _) => replaced++;

        viewModel.FileName = Path.Combine("dir", "notes.txt");
        viewModel.SelectionOnly = true;
        viewModel.Title.Should().Be(FakeDialogHost.TranslatedMark + "Find - notes.txt (" + FakeDialogHost.TranslatedMark
                                    + "selection only)");
        viewModel.ShowsHighlightAll.Should().BeTrue();
        viewModel.ReplaceMode = true;
        viewModel.ShowsHighlightAll.Should().BeFalse("Replace takes the place of highlighting");
        viewModel.FindNextCommand.Execute(null);
        viewModel.FindPreviousCommand.Execute(null);
        viewModel.ReplaceCommand.Execute(null);

        searches.Should().Equal(false, true);
        replaced.Should().Be(1);
    }

    [Test]
    public async Task EditorViewModel_should_ask_before_closing_with_changes_and_save_as_read()
    {
        string path = Path.Combine(_folder, "todo");
        await File.WriteAllTextAsync(path, "pick 1\r\npick 2\r\n");
        FakeDialogHost host = new();
        EditorViewModel viewModel = new(host, path, showWarning: false,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        await viewModel.LoadAsync();
        viewModel.IsReadOnly.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse("nothing changed");
        viewModel.Text = viewModel.Text!.Replace("pick 2", "drop 2");
        viewModel.OnClosing().Should().BeTrue("the change is not saved");
        viewModel.AsksToSave.Should().BeTrue();
        await viewModel.SaveAndCloseCommand.ExecuteAsync(null);

        (await File.ReadAllTextAsync(path)).Should().Be("pick 1\r\ndrop 2\r\n", "the file keeps its line endings");
        viewModel.Accepted.Should().BeTrue();
        host.IsClosed.Should().BeTrue();
    }

    [Test]
    public async Task EditorViewModel_should_reject_the_file_when_the_changes_are_discarded()
    {
        string path = Path.Combine(_folder, "message");
        await File.WriteAllTextAsync(path, "subject\n");
        EditorViewModel viewModel = new(new FakeDialogHost(), path, showWarning: true,
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await viewModel.LoadAsync();

        viewModel.Text = "";
        viewModel.DiscardCommand.Execute(null);

        viewModel.Accepted.Should().BeFalse();
        viewModel.OnClosing().Should().BeFalse("the close is decided");
        (await File.ReadAllTextAsync(path)).Should().Be("subject\n");
    }

    [Test]
    public async Task DeleteRemoteBranchViewModel_should_need_the_remote_check_box_and_offer_the_tracking_branches()
    {
        IGitModule module = Substitute.For<IGitModule>();
        IGitRef remote = Ref("origin/feature", "origin", "feature", isRemote: true);
        IGitRef head = Ref("feature", "", "feature", isRemote: false);
        head.IsTrackingRemote(remote).Returns(true);
        remote.CompleteName.Returns("refs/remotes/origin/feature");
        module.GetRefs(RefsFilter.Remotes).Returns([remote]);
        module.GetRefs(RefsFilter.Heads).Returns([head]);
        module.GetMergedRemoteBranches().Returns(["refs/remotes/origin/feature"]);
        List<(string Remote, IReadOnlyList<string> Branches)> deleted = [];
        List<IReadOnlyList<string>> localDeletes = [];
        FakeDialogHost host = new();
        DeleteRemoteBranchViewModel viewModel = new(host, module, "origin/feature",
            (name, branches) =>
            {
                deleted.Add((name, branches));
                return Task.FromResult(true);
            },
            branches =>
            {
                localDeletes.Add(branches);
                return Task.CompletedTask;
            },
            null);

        await viewModel.LoadAsync();
        viewModel.DeleteCommand.CanExecute(null).Should().BeFalse("upstream's check box enables Delete");
        viewModel.CanDeleteLocalTrackingBranch.Should().BeTrue();
        viewModel.DeleteRemote = true;
        viewModel.DeleteLocalTrackingBranch = true;
        await viewModel.DeleteCommand.ExecuteAsync(null);

        deleted.Should().ContainSingle().Which.Remote.Should().Be("origin");
        localDeletes.Should().ContainSingle().Which.Should().Equal("feature");
        host.Questions.Should().BeEmpty("the branch is merged");
        host.Result.Should().Be(true);
    }

    private static IGitRef Ref(string name, string remote, string localName, bool isRemote)
    {
        IGitRef gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns(name);
        gitRef.Remote.Returns(remote);
        gitRef.LocalName.Returns(localName);
        gitRef.IsRemote.Returns(isRemote);
        gitRef.IsHead.Returns(!isRemote);
        return gitRef;
    }
}
