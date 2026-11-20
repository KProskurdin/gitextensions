using AwesomeAssertions;
using GitExtensions.Xplat.Core.Operations;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

// Upstream's FormInit.
internal sealed class InitRepositoryViewModelTests
{
    private readonly List<(string Folder, bool Central)> _inits = [];
    private FakeDialogHost _host = null!;
    private string _folder = null!;

    [SetUp]
    public void Setup()
    {
        _inits.Clear();
        _host = new FakeDialogHost();
        _folder = Directory.CreateTempSubdirectory("xplat-init-").FullName;
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task CreateAsync_should_make_a_personal_repository_and_show_gits_answer()
    {
        InitRepositoryViewModel viewModel = NewViewModel(Path.Combine(_folder, "new"));

        await viewModel.CreateCommand.ExecuteAsync(null);

        _inits.Should().Equal((Path.Combine(_folder, "new"), false));
        _host.Messages.Should().ContainSingle().Which.Should().Contain("Initialized empty Git repository");
        viewModel.CreatedRepository.Should().Be(Path.Combine(_folder, "new"));
        _host.IsClosed.Should().BeTrue();
        _host.Result.Should().Be(true);
    }

    [Test]
    public async Task CreateAsync_should_make_a_central_repository_bare_and_shared()
    {
        InitRepositoryViewModel viewModel = NewViewModel(_folder);
        viewModel.Personal.Should().BeTrue("upstream starts on a personal repository");

        viewModel.Central = true;
        await viewModel.CreateCommand.ExecuteAsync(null);

        viewModel.Personal.Should().BeFalse();
        _inits.Should().Equal((_folder, true));
    }

    [TestCase("")]
    [TestCase("relative/folder")]
    public async Task CreateAsync_should_ask_for_a_rooted_folder_and_stay_open(string folder)
    {
        InitRepositoryViewModel viewModel = NewViewModel(folder);

        await viewModel.CreateCommand.ExecuteAsync(null);

        _inits.Should().BeEmpty();
        _host.Messages.Should().ContainSingle().Which.Should()
            .Be(FakeDialogHost.TranslatedMark + InitRepositoryViewModel.ChooseDirectoryText);
        _host.IsClosed.Should().BeFalse();
    }

    [Test]
    public async Task CreateAsync_should_refuse_a_file()
    {
        string file = Path.Combine(_folder, "file.txt");
        File.WriteAllText(file, "");
        InitRepositoryViewModel viewModel = NewViewModel(file);

        await viewModel.CreateCommand.ExecuteAsync(null);

        _inits.Should().BeEmpty();
        _host.Messages.Should().ContainSingle().Which.Should()
            .Be(FakeDialogHost.TranslatedMark + InitRepositoryViewModel.ChooseDirectoryNotFileText);
    }

    [Test]
    public async Task BrowseAsync_should_take_the_folder_chosen()
    {
        InitRepositoryViewModel viewModel = NewViewModel("");
        viewModel.PickFolder = () => Task.FromResult<string?>(_folder);

        await viewModel.BrowseCommand.ExecuteAsync(null);

        viewModel.Directory.Should().Be(_folder);
    }

    private InitRepositoryViewModel NewViewModel(string directory)
        => new(_host, directory, ["/recent/one"], (folder, central) =>
        {
            _inits.Add((folder, central));
            return Task.FromResult($"Initialized empty Git repository in {folder}/.git/");
        });
}
