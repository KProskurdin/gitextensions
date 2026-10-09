using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Xplat.Core.Operations;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  Where a git setting is read from and written to: the user's global config, or one repository's own config.
/// </summary>
public enum ConfigScope
{
    Global,
    Local,
}

/// <summary>
///  Reads and writes git config values (the "real git config" world, not the app's settings file). Implementations run the
///  work off the calling thread and throw <see cref="GitOperationException"/> with git's message when git fails.
/// </summary>
public interface IGitConfigService
{
    /// <summary>
    ///  The value of <paramref name="key"/> in <paramref name="scope"/> only, or empty when it is not set there.
    /// </summary>
    Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null);

    /// <summary>
    ///  Every value of a multi-valued <paramref name="key"/> in <paramref name="scope"/> only, in the order git applies them.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllAsync(ConfigScope scope, string key, string? repositoryPath = null);

    /// <summary>
    ///  Sets <paramref name="key"/> in <paramref name="scope"/>. An empty value removes the key, so the next scope applies.
    /// </summary>
    Task SetAsync(ConfigScope scope, string key, string value, string? repositoryPath = null);
}

public sealed class GitConfigService : IGitConfigService
{
    // git config --get exits with 1 when the key is not set; that is not an error here.
    private const int KeyNotSetExitCode = 1;

    // git config --unset of a key that is not set exits with 5.
    private const int ConfigUnsetMissingExitCode = 5;

    public Task<string> GetAsync(ConfigScope scope, string key, string? repositoryPath = null)
        => Task.Run(() =>
        {
            ExecutionResult result = Executable(repositoryPath).Execute(
                new GitArgumentBuilder("config") { ScopeOption(scope), "--get", key.Quote() }, throwOnErrorExit: false);
            if (result.ExitCode == KeyNotSetExitCode)
            {
                return "";
            }

            ThrowOnError(result, key);
            return result.StandardOutput.TrimEnd('\r', '\n');
        });

    public Task<IReadOnlyList<string>> GetAllAsync(ConfigScope scope, string key, string? repositoryPath = null)
        => Task.Run<IReadOnlyList<string>>(() =>
        {
            ExecutionResult result = Executable(repositoryPath).Execute(
                new GitArgumentBuilder("config") { ScopeOption(scope), "-z", "--get-all", key.Quote() },
                throwOnErrorExit: false);
            if (result.ExitCode == KeyNotSetExitCode)
            {
                return [];
            }

            ThrowOnError(result, key);
            return result.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        });

    public Task SetAsync(ConfigScope scope, string key, string value, string? repositoryPath = null)
        => Task.Run(() =>
        {
            GitArgumentBuilder arguments = value.Length == 0
                ? new GitArgumentBuilder("config") { ScopeOption(scope), "--unset", key.Quote() }
                : new GitArgumentBuilder("config") { ScopeOption(scope), key.Quote(), value.Quote() };
            ExecutionResult result = Executable(repositoryPath).Execute(arguments, throwOnErrorExit: false);
            if (value.Length == 0 && result.ExitCode == ConfigUnsetMissingExitCode)
            {
                return;
            }

            ThrowOnError(result, key);
        });

    private static string ScopeOption(ConfigScope scope) => scope == ConfigScope.Global ? "--global" : "--local";

    // Global settings need no repository; git is run in the user's home folder so a repository there is not involved.
    private static IExecutable Executable(string? repositoryPath)
        => new Executable(AppSettings.GitCommand,
            repositoryPath ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private static void ThrowOnError(ExecutionResult result, string key)
    {
        if (!result.ExitedSuccessfully)
        {
            string error = result.StandardError.Trim();
            throw new GitOperationException(error.Length > 0
                ? error
                : $"git config {key} failed with exit code {result.ExitCode}");
        }
    }
}
