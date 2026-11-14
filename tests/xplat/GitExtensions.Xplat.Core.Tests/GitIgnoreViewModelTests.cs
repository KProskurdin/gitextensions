using AwesomeAssertions;
using GitExtensions.Xplat.Core.Repository;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class GitIgnoreViewModelTests
{
    private string _folder = null!;
    private string _file = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "xplat-vm-" + Guid.NewGuid().ToString("N")))
            .FullName;
        _file = Path.Combine(_folder, ".gitignore");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task GitIgnoreViewModel_should_read_the_file_save_it_and_close()
    {
        await File.WriteAllTextAsync(_file, "bin/\n");
        FakeDialogHost host = new();
        GitIgnoreViewModel viewModel = NewViewModel(host);
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse("nothing is read yet");

        await viewModel.LoadAsync();
        viewModel.Text.Should().Be("bin/\n");
        viewModel.NewLine.Should().Be("\n");
        viewModel.Text = "bin/\nobj/\n";
        viewModel.MayClose.Should().BeFalse();
        await viewModel.SaveCommand.ExecuteAsync(null);

        (await File.ReadAllTextAsync(_file)).Should().Be("bin/\nobj/\n");
        viewModel.Saved.Should().BeTrue();
        host.IsClosed.Should().BeTrue();
        viewModel.Title.Should().Be(FakeDialogHost.TranslatedMark + "Edit .gitignore");
    }

    [Test]
    public async Task GitIgnoreViewModel_should_keep_the_window_open_when_the_question_is_cancelled()
    {
        await File.WriteAllTextAsync(_file, "bin/\n");
        FakeDialogHost host = new() { ThreeWayAnswer = null };
        GitIgnoreViewModel viewModel = NewViewModel(host);
        await viewModel.LoadAsync();
        viewModel.Text = "obj/\n";

        await viewModel.AskToCloseAsync();

        host.IsClosed.Should().BeFalse();
        viewModel.Saved.Should().BeFalse();
        (await File.ReadAllTextAsync(_file)).Should().Be("bin/\n");
    }

    [Test]
    public async Task GitIgnoreViewModel_should_close_a_bare_repository_with_upstreams_message()
    {
        FakeDialogHost host = new();
        GitIgnoreViewModel viewModel = new(host, _file, localExclude: false, () => true, () => Task.FromResult(false));

        await viewModel.LoadAsync();

        host.Messages.Should().ContainSingle();
        host.IsClosed.Should().BeTrue();
        viewModel.IsLoaded.Should().BeFalse();
    }

    [Test]
    public async Task GitIgnoreViewModel_should_read_the_file_again_after_add_pattern()
    {
        await File.WriteAllTextAsync(_file, "bin/\n");
        GitIgnoreViewModel viewModel = new(new FakeDialogHost(), _file, localExclude: false, () => false,
            async () =>
            {
                await File.AppendAllTextAsync(_file, "*.dll\n");
                return true;
            });
        await viewModel.LoadAsync();

        await viewModel.AddPatternCommand.ExecuteAsync(null);

        viewModel.Text.Should().Be("bin/\n*.dll\n");
        viewModel.Saved.Should().BeTrue();
    }

    private GitIgnoreViewModel NewViewModel(FakeDialogHost host)
        => new(host, _file, localExclude: false, () => false, () => Task.FromResult(false));
}
