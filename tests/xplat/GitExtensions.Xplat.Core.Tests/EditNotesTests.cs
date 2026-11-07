using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class EditNotesTests
{
    private const string RepositoryPath = "/work/one";
    private const string Editor = "'/apps/GitExtensions' fileeditor";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private FakeGitOperations _git = null!;
    private RepositoryOperationsViewModel _viewModel = null!;

    [SetUp]
    public void Setup()
    {
        _git = new FakeGitOperations();
        _viewModel = new RepositoryOperationsViewModel(_git);
    }

    [Test]
    public async Task EditNotesAsync_should_edit_the_commits_notes_in_the_editor_and_report_the_change()
    {
        _viewModel.EditorCommand = Editor;
        int changed = 0;
        _viewModel.RepositoryChanged += (_, _) => changed++;

        Task<bool> edit = _viewModel.EditNotesAsync(RepositoryPath, Commit);
        _git.NameAt(0).Should().Be("EditNotes");
        _git.ArgumentsAt(0).Should().Be($"{RepositoryPath} {Commit} {Editor}");
        _git.Complete(0);

        (await edit).Should().BeTrue();
        changed.Should().Be(1, "the commit info reads the notes again");
    }

    [Test]
    public async Task EditNotesAsync_without_an_editor_should_not_run_git()
    {
        (await _viewModel.EditNotesAsync(RepositoryPath, Commit)).Should().BeFalse();

        _git.Count.Should().Be(0);
        _viewModel.ErrorMessage.Should().Be("No editor is available for the notes.");
    }
}
