using AwesomeAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using GitCommands.UserRepositoryHistory.Legacy;
using NSubstitute;
using NUnit.Framework;
using IRepositoryStorage = GitCommands.UserRepositoryHistory.IRepositoryStorage;
using Repository = GitCommands.UserRepositoryHistory.Repository;

namespace GitCommandsTests.UserRepositoryHistory;

/// <summary>
///  POSIX counterparts of the <c>AddAsMostRecentAsync</c> tests in <c>LocalRepositoryManagerTests</c>, which use Windows
///  paths with a trailing '\'. Same structure and expectations.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
// Mirrors: LocalRepositoryManagerTests
internal sealed class XplatLinuxLocalRepositoryManagerTests
{
    private const string KeyRecentHistory = "history";
    private const string RepoToAdd = "/path to add/";
    private IRepositoryStorage _repositoryStorage = null!;
    private LocalRepositoryManager _manager = null!;
    private int _userSetting;

    [SetUp]
    public void Setup()
    {
        _userSetting = AppSettings.RecentRepositoriesHistorySize;
        AppSettings.RecentRepositoriesHistorySize = 30;

        _repositoryStorage = Substitute.For<IRepositoryStorage>();
        _manager = new LocalRepositoryManager(_repositoryStorage, Substitute.For<IRepositoryHistoryMigrator>());
    }

    [TearDown]
    public void TearDown()
    {
        AppSettings.RecentRepositoriesHistorySize = _userSetting;
    }

    [Test]
    public async Task AddAsMostRecentAsync_should_add_new_path_as_top_entry()
    {
        List<Repository> history =
        [
            new Repository("/path1/"),
            new Repository("/path3/"),
            new Repository("/path4/"),
            new Repository("/path5/"),
        ];
        _repositoryStorage.Load(KeyRecentHistory).Returns(x => history);

        IList<Repository> newHistory = await _manager.AddAsMostRecentAsync(RepoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(RepoToAdd);
    }

    [Test]
    public async Task AddAsMostRecentAsync_should_move_existing_path_as_top_entry()
    {
        List<Repository> history =
        [
            new Repository("/path1/"),
            new Repository("/path3/"),
            new Repository("/path4/"),
            new Repository(RepoToAdd),
            new Repository("/path5/"),
        ];
        _repositoryStorage.Load(KeyRecentHistory).Returns(x => history);

        IList<Repository> newHistory = await _manager.AddAsMostRecentAsync(RepoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(RepoToAdd);
    }

    [Test]
    public async Task AddAsMostRecentAsync_should_move_only_first_existing_path_as_top_entry()
    {
        List<Repository> history =
        [
            new Repository("/path1/"),
            new Repository("/path3/"),
            new Repository(RepoToAdd),
            new Repository("/path4/"),
            new Repository(RepoToAdd),
            new Repository("/path5/"),
        ];
        _repositoryStorage.Load(KeyRecentHistory).Returns(x => history);

        IList<Repository> newHistory = await _manager.AddAsMostRecentAsync(RepoToAdd);

        newHistory.Should().HaveCount(6);
        newHistory[0].Path.Should().Be(RepoToAdd);
        newHistory[4].Path.Should().Be(RepoToAdd);
    }

    [Test]
    public async Task AddAsMostRecentAsync_should_not_move_if_path_already_as_top_entry()
    {
        List<Repository> history =
        [
            new Repository(RepoToAdd),
            new Repository("/path1/"),
            new Repository("/path3/"),
            new Repository("/path4/"),
            new Repository("/path5/"),
        ];
        _repositoryStorage.Load(KeyRecentHistory).Returns(x => history);

        IList<Repository> newHistory = await _manager.AddAsMostRecentAsync(RepoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(RepoToAdd);
        _repositoryStorage.DidNotReceive().Save(KeyRecentHistory, Arg.Any<IList<Repository>>());
    }
}
