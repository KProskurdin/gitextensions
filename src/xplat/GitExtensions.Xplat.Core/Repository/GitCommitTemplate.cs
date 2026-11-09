using System.IO;
using System.IO.Abstractions;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The outcome of reading git's <c>commit.template</c>: the template (null when none is set), or upstream's error for the
///  commit window to show.
/// </summary>
public sealed record CommitTemplateResult(string? Template, string? ErrorCaption = null, string? Error = null);

/// <summary>
///  Upstream's <c>FormCommit.AssignCommitMessageFromTemplate</c>: the commit window starts from git's <c>commit.template</c>
///  file when it has no draft, read by upstream's <see cref="CommitTemplateManager.LoadGitCommitTemplate"/>.
/// </summary>
public static class GitCommitTemplate
{
    /// <summary>
    ///  Upstream's <c>_templateNotFoundCaption</c>.
    /// </summary>
    public const string NotFoundCaption = "Template Error";

    /// <summary>
    ///  Upstream's <c>_templateLoadErrorCaption</c>.
    /// </summary>
    public const string LoadErrorCaption = "Template could not be loaded";

    /// <summary>
    ///  Upstream's <c>_templateNotFound</c>, with the file's name for {0}.
    /// </summary>
    public static string NotFound { get; } =
        $"Template not found: {{0}}.{Environment.NewLine}{Environment.NewLine}You can set your template:{Environment.NewLine}"
        + $"\t$ git config commit.template ./.git_commit_msg.txt{Environment.NewLine}You can unset the template:"
        + $"{Environment.NewLine}\t$ git config --unset commit.template";

    public static Task<CommitTemplateResult> LoadAsync(string repositoryPath)
        => Task.Run(() => Load(new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)));

    /// <summary>
    ///  Reads the template of <paramref name="module"/>. Unlike upstream, a path starting with "~/" is in the home folder,
    ///  as git reads it (<c>git config --path</c>); upstream would look for a "~" folder in the working directory.
    /// </summary>
    public static CommitTemplateResult Load(GitModule module)
    {
        CommitTemplateManager manager = new(() => module, new HomeFullPathResolver(module.WorkingDir), new FileSystem());
        try
        {
            return new CommitTemplateResult(manager.LoadGitCommitTemplate());
        }
        catch (FileNotFoundException ex)
        {
            return new CommitTemplateResult(null, NotFoundCaption, string.Format(NotFound, ex.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new CommitTemplateResult(null, LoadErrorCaption, ex.Message);
        }
    }

    // Git's own path expansion of "~/" before upstream's FullPathResolver, which resolves the rest against the working
    // directory.
    private sealed class HomeFullPathResolver(string workingDir) : IFullPathResolver
    {
        private readonly FullPathResolver _resolver = new(() => workingDir);

        public string? Resolve(string? path)
            => path is ['~', '/' or '\\', .. string rest] && EnvironmentConfiguration.GetHomeDir() is { Length: > 0 } home
                ? Path.Combine(home, rest)
                : _resolver.Resolve(path);
    }
}
