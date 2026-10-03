using AwesomeAssertions;
using GitCommands;
using GitCommands.UserRepositoryHistory;
using NUnit.Framework;

namespace GitCommandsTests.UserRepositoryHistory;

/// <summary>
///  POSIX counterparts of <c>RecentRepoSplitterTests</c>, which uses Windows paths. Same structure and expectations.
/// </summary>
[Platform(Include = "Linux,MacOsX")]
internal sealed class XplatLinuxRecentRepoSplitterTests
{
    private const string TopPath1 = "/this/is/a/repo_anchored_in_top_path1/";
    private const string TopPath2 = "/this/is/a/repo_anchored_in_top_path2/";
    private const string RecentPath = "/this/is/a/repo_anchored_in_recent_path/";
    private const string NotAnchoredPath = "/this/is/a/repo_not_anchored_path/";

    private static readonly string _homeRelativeRepoPath = Path.Combine(".gitext-xplat-test", "this", "is", "a", "very_very_very", "long", "repo_path");

    [Test]
    public void SplitRecentRepos_Should_use_most_significant_folder_as_caption()
    {
        List<Repository> history = [new Repository(TopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }];
        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        recentRepoList.Should().ContainSingle();
    }

    [Test]
    public void SplitRecentRepos_Should_not_shorten_as_caption()
    {
        List<Repository> history = [new Repository(TopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }];
        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.None };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().Be(TopPath1);
        recentRepoList.Should().ContainSingle();
    }

    [Test]
    public void SplitRecentRepos_Should_not_shorten_but_handle_user_folder_as_caption()
    {
        // The user's home folder is shown as "~", the POSIX counterpart of the Windows profile.
        string repoPath = Path.Combine(PathUtil.UserProfilePath, _homeRelativeRepoPath);
        List<Repository> history = [new Repository(repoPath) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }];
        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.None };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().StartWith("~/.gitext-xplat-test").And.EndWith("very_very_very/long/repo_path");
        recentRepoList.Should().ContainSingle();
    }

    [Test]
    public void SplitRecentRepos_Should_display_middle_dots_in_caption()
    {
        // Only an existing folder path can be shortened.
        string repoPath = Path.Combine(PathUtil.UserProfilePath, _homeRelativeRepoPath);
        Directory.CreateDirectory(repoPath);
        try
        {
            List<Repository> history = [new Repository(repoPath) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }];
            RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.MiddleDots };
            List<RecentRepoInfo> topRepoList = [];
            List<RecentRepoInfo> recentRepoList = [];

            sut.SplitRecentRepos(history, topRepoList, recentRepoList);

            topRepoList.Should().ContainSingle();
            topRepoList[0].Caption.Should().Be("~/.gitext-xplat-test/../long/repo_path");
            recentRepoList.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(Path.Combine(PathUtil.UserProfilePath, ".gitext-xplat-test"), recursive: true);
        }
    }

    [Test]
    public void SplitRecentRepos_Should_split_depending_anchor()
    {
        List<Repository> history =
        [
            new Repository(TopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new Repository(TopPath2) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new Repository(RecentPath) { Anchor = Repository.RepositoryAnchor.AnchoredInRecent },
            new Repository(NotAnchoredPath) { Anchor = Repository.RepositoryAnchor.None },
        ];
        RecentRepoSplitter sut = new()
        {
            ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir,
            SortTopRepos = false,
            SortRecentRepos = false
        };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().HaveCount(2);
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        topRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList.Should().HaveCount(4);
        recentRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        recentRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList[2].Caption.Should().Be("repo_anchored_in_recent_path");
        recentRepoList[3].Caption.Should().Be("repo_not_anchored_path");
    }

    [Test]
    public void SplitRecentRepos_Should_split_depending_anchor_and_sort_alphabetically()
    {
        List<Repository> history =
        [
            // Unsorted!
            new Repository(NotAnchoredPath) { Anchor = Repository.RepositoryAnchor.None },
            new Repository(RecentPath) { Anchor = Repository.RepositoryAnchor.AnchoredInRecent },
            new Repository(TopPath2) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new Repository(TopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
        ];
        RecentRepoSplitter sut = new()
        {
            ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir,
            SortTopRepos = true,
            SortRecentRepos = true
        };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().HaveCount(2);
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        topRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList.Should().HaveCount(4);
        recentRepoList[0].Caption.Should().Be("repo_anchored_in_recent_path");
        recentRepoList[1].Caption.Should().Be("repo_anchored_in_top_path1");
        recentRepoList[2].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList[3].Caption.Should().Be("repo_not_anchored_path");
    }

    [Test]
    public void SplitRecentRepos_Should_split_depending_anchor_and_sort_alphabetically_Hiding_Top_Repo_In_Recent_list()
    {
        List<Repository> history =
        [
            // Unsorted!
            new Repository(NotAnchoredPath) { Anchor = Repository.RepositoryAnchor.None },
            new Repository(RecentPath) { Anchor = Repository.RepositoryAnchor.AnchoredInRecent },
            new Repository(TopPath2) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new Repository(TopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
        ];
        RecentRepoSplitter sut = new()
        {
            ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir,
            SortTopRepos = true,
            SortRecentRepos = true,
            HideTopRepositoriesFromRecentList = true
        };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().HaveCount(2);
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        topRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList.Should().HaveCount(2);
        recentRepoList[0].Caption.Should().Be("repo_anchored_in_recent_path");
        recentRepoList[1].Caption.Should().Be("repo_not_anchored_path");
    }
}
