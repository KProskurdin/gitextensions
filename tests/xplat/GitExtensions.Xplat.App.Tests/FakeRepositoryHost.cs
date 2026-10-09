using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;

namespace GitExtensions.Xplat.App.Tests;

/// <summary>
///  A repository host plugin with fixed repositories and pull requests, so the repository host windows are tested without a
///  network service.
/// </summary>
internal sealed class FakeRepositoryHost : GitPluginBase, IRepositoryHostPlugin
{
    public FakeRepositoryHost()
        : base(hasSettings: false)
    {
        Id = new Guid("8F0B7A9E-3C55-4C1D-9A25-6E3B7E7C1F42");
        Name = "FakeHub";
        Description = Name;
    }

    public List<IHostedRepository> MyRepos { get; } = [];

    public List<IHostedRepository> SearchResults { get; } = [];

    public List<IHostedRemote> Remotes { get; } = [];

    public string? UpstreamRemoteToAdd { get; set; }

    public bool ConfigurationOk => true;

    public string? OwnerLogin => "me";

    public override bool Execute(GitUIEventArgs args) => false;

    public IReadOnlyList<IHostedRepository> SearchForRepository(string search) => SearchResults;

    public IReadOnlyList<IHostedRepository> GetRepositoriesOfUser(string user) => SearchResults;

    public IHostedRepository GetRepository(string user, string repositoryName) => MyRepos.First(repo => repo.Name == repositoryName);

    public IReadOnlyList<IHostedRepository> GetMyRepos() => MyRepos;

    public void ConfigureContextMenu(ContextMenuStrip contextMenu)
    {
    }

    public bool GitModuleIsRelevantToMe() => Remotes.Count > 0;

    public IReadOnlyList<IHostedRemote> GetHostedRemotesForModule() => Remotes;

    public Task<string?> AddUpstreamRemoteAsync() => Task.FromResult(UpstreamRemoteToAdd);
}

internal sealed class FakeHostedRemote(string name, IHostedRepository repository, bool isOwnedByMe) : IHostedRemote
{
    public string? Name => name;

    public string Data => $"{repository.Owner}/{repository.Name}";

    public string DisplayData => $"{name} ({Data})";

    public bool IsOwnedByMe => isOwnedByMe;

    public string Owner => repository.Owner ?? "";

    public string RemoteRepositoryName => repository.Name;

    public string RemoteUrl => repository.CloneUrl;

    public GitProtocol CloneProtocol => GitProtocol.Https;

    public IHostedRepository GetHostedRepository() => repository;

    public string GetBlameUrl(string commitHash, string fileName, int lineIndex) => "";
}

internal sealed class FakeHostedRepository(string owner, string name, string cloneUrl) : IHostedRepository
{
    public string? Owner => owner;

    public string Name => name;

    public string Description { get; init; } = "";

    public bool IsAFork => ParentUrl is not null;

    public bool IsMine { get; init; }

    public bool IsPrivate { get; init; }

    public int Forks { get; init; }

    public string Homepage { get; init; } = "";

    public string? ParentUrl { get; init; }

    public string? ParentOwner { get; init; }

    public string CloneUrl => cloneUrl;

    public List<string> Branches { get; } = [];

    public string DefaultBranch { get; set; } = "";

    public List<IPullRequestInformation> PullRequests { get; } = [];

    public List<(string MyBranch, string RemoteBranch, string Title, string Body)> CreatedPullRequests { get; } = [];

    public int ForkCount { get; private set; }

    public GitProtocol CloneProtocol { get; set; } = GitProtocol.Https;

    public IReadOnlyList<GitProtocol> SupportedCloneProtocols { get; set; } = [];

    public IReadOnlyList<IHostedBranch> GetBranches()
        => [.. Branches.Select(branch => new FakeHostedBranch(branch))];

    public string GetDefaultBranch() => DefaultBranch;

    public IHostedRepository Fork()
    {
        ForkCount++;
        return this;
    }

    public IReadOnlyList<IPullRequestInformation> GetPullRequests() => PullRequests;

    public int CreatePullRequest(string myBranch, string remoteBranch, string title, string body)
    {
        CreatedPullRequests.Add((myBranch, remoteBranch, title, body));
        return CreatedPullRequests.Count;
    }

    private sealed record FakeHostedBranch(string Name) : IHostedBranch
    {
        public ObjectId Sha => ObjectId.WorkTreeId;
    }
}

internal sealed class FakePullRequest(IHostedRepository headRepo, string headRef, string diff) : IPullRequestInformation
{
    public string Title { get; init; } = "";

    public string Body { get; init; } = "";

    public string Owner { get; init; } = "";

    public DateTime Created { get; init; } = new(2026, 10, 1, 12, 0, 0);

    public IHostedRepository BaseRepo => headRepo;

    public IHostedRepository HeadRepo => headRepo;

    public string BaseSha { get; init; } = "";

    public string HeadSha { get; init; } = "";

    public string BaseRef => "main";

    public string HeadRef => headRef;

    public string Id { get; init; } = "";

    public string DetailedInfo => "";

    public string FetchBranch { get; init; } = "";

    public FakeDiscussion Discussion { get; } = new();

    public bool IsClosed { get; private set; }

    public Task<string> GetDiffDataAsync() => Task.FromResult(diff);

    public void Close() => IsClosed = true;

    public IPullRequestDiscussion GetDiscussion() => Discussion;
}

internal sealed class FakeDiscussion : IPullRequestDiscussion
{
    public List<IDiscussionEntry> Entries { get; } = [];

    public List<string> Posted { get; } = [];

    public void Post(string data)
    {
        Posted.Add(data);
        Entries.Add(new FakeDiscussionEntry("me", data));
    }

    public void ForceReload()
    {
    }
}

internal sealed record FakeDiscussionEntry(string? Author, string? Body) : IDiscussionEntry
{
    public DateTime Created => new(2026, 10, 2, 9, 30, 0);
}

internal sealed record FakeCommitEntry(string? Author, string? Body, string? Sha) : ICommitDiscussionEntry
{
    public DateTime Created => new(2026, 10, 2, 9, 0, 0);
}
