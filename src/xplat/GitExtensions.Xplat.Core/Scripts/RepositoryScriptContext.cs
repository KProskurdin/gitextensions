using GitCommands;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  A script's variables taken from an open repository: the commit selected in the grid, HEAD, the config and the
///  repository's name, read as upstream's <c>ScriptOptionsParser</c> reads them (<c>GitModule.GetRevision</c> with refs,
///  <c>GetCurrentRemote</c>, <c>RepositoryDescriptionProvider</c>). Create it off the UI thread: it runs git.
/// </summary>
public sealed class RepositoryScriptContext : IScriptContext
{
    private readonly GitModule _module;
    private readonly Func<IReadOnlyList<string>, Task<string>> _choose;

    /// <param name="selectedHashes">The commits selected in the grid, the last selected one last; empty without a grid.</param>
    /// <param name="choose">Asks the user to pick one of several refs or remotes.</param>
    /// <param name="extraOptions">Options the place the script runs from adds (e.g. SelectedRelativePaths).</param>
    public RepositoryScriptContext(string repositoryPath, IReadOnlyList<string> selectedHashes,
        Func<IReadOnlyList<string>, Task<string>> choose,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? extraOptions = null)
    {
        GitDirectoryResolver resolver = new();
        _module = new GitModule(new GitExecutorProvider(resolver), repositoryPath);
        _choose = choose;
        SelectedHashes = selectedHashes;
        ExtraOptions = extraOptions ?? new Dictionary<string, IReadOnlyList<string>>();
        RepoName = new RepositoryDescriptionProvider(resolver).Get(_module.WorkingDir, isValidGitWorkingDir: null);
        CurrentBranch = _module.GetSelectedBranch(emptyIfDetached: true);
        CurrentRemote = _module.GetCurrentRemote();
        Selected = selectedHashes.Count > 0 && ObjectId.TryParse(selectedHashes[^1], out ObjectId selected)
            ? ToScriptRevision(_module.GetRevision(selected, shortFormat: false, loadRefs: true))
            : null;
    }

    public string WorkingDir => _module.WorkingDir;

    public string RepoName { get; }

    public ScriptRevision? Selected { get; }

    public IReadOnlyList<string> SelectedHashes { get; }

    public string CurrentBranch { get; }

    public string CurrentRemote { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ExtraOptions { get; }

    public Task<ScriptRevision?> GetCurrentAsync(bool loadBody)
        => Task.Run(() =>
        {
            try
            {
                return (ScriptRevision?)ToScriptRevision(_module.GetRevision(shortFormat: !loadBody, loadRefs: true));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A repository without commits has no HEAD; the script is then aborted, as upstream does.
                return null;
            }
        });

    public string GetConfig(string key) => _module.GetEffectiveSetting(key);

    /// <summary>
    ///  The commit hash <paramref name="target"/> (a hash or ref a <c>navigateTo:</c> script printed) names, or null.
    /// </summary>
    public static Task<string?> ResolveCommitAsync(string repositoryPath, string target)
        => Task.Run(() =>
        {
            GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath);
            string hash = module.GitExecutable
                .GetOutput(new GitArgumentBuilder("rev-parse") { "--verify", "-q", $"{target}^{{commit}}".Quote() })
                .Trim();
            return hash.Length == 0 ? null : hash;
        });

    public Task<string> ChooseAsync(IReadOnlyList<string> options) => _choose(options);

    private static ScriptRevision ToScriptRevision(GitRevision revision)
        => new(revision.Guid, revision.Subject, revision.Body, revision.Author ?? "", revision.Committer ?? "",
            revision.AuthorDate, revision.CommitDate,
            [
                .. revision.Refs
                    .Where(gitRef => gitRef.IsTag || gitRef.IsHead || gitRef.IsRemote)
                    .Select(gitRef => new ScriptRef(gitRef.Name, gitRef.IsTag, gitRef.IsRemote, gitRef.Remote))
            ]);
}
