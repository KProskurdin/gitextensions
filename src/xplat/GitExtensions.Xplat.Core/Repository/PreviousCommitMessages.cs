using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Configurations;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream <c>FormCommit</c>'s "Commit message" menu: earlier messages to start from, and a message listing the commits that
///  staged submodule changes bring in.
/// </summary>
public static class PreviousCommitMessages
{
    // Upstream's AddCommitMessageToMenu.
    private const int MaxLabelLength = 72;

    private const string SubprojectCommit = "Subproject commit ";

    /// <summary>
    ///  The messages of the last <paramref name="count"/> commits on HEAD, with <paramref name="onlyMine"/> only those by the
    ///  user git config names (upstream's "Show only my messages"), newest first and without trailing newlines.
    /// </summary>
    public static Task<IReadOnlyList<string>> ReadAsync(string repositoryPath, int count, bool onlyMine)
        => Task.Run<IReadOnlyList<string>>(() =>
        {
            GitModule module = Module(repositoryPath);
            string authorPattern = onlyMine
                ? AuthorPattern(module.GetEffectiveSetting(SettingKeyString.UserName),
                    module.GetEffectiveSetting(SettingKeyString.UserEmail))
                : string.Empty;
            return
            [
                .. module.GetPreviousCommitMessages(count, "HEAD", authorPattern)
                    .WhereNotNull()
                    .Select(message => message.TrimEnd('\n'))
                    .Where(message => !string.IsNullOrWhiteSpace(message))
            ];
        });

    /// <summary>
    ///  Upstream's <c>--author</c> pattern for "Show only my messages": exactly the configured name and e-mail.
    /// </summary>
    public static string AuthorPattern(string userName, string userEmail)
        => $"^{Regex.Escape(userName)} <{Regex.Escape(userEmail)}>$";

    /// <summary>
    ///  The menu's messages, as upstream's <c>CommitMessageToolStripMenuItemDropDownOpening</c>: the message of the last commit
    ///  made from the commit window (in any repository) comes first unless the list has it, and the list keeps at most
    ///  <paramref name="maxCount"/> entries.
    /// </summary>
    public static IReadOnlyList<string> Merge(IEnumerable<string> previous, string lastCommitMessage, int maxCount)
    {
        List<string> messages = [.. previous];
        if (!string.IsNullOrWhiteSpace(lastCommitMessage) && !messages.Contains(lastCommitMessage))
        {
            if (messages.Count >= maxCount && messages.Count > 0)
            {
                messages.RemoveRange(Math.Max(maxCount - 1, 0), messages.Count - Math.Max(maxCount - 1, 0));
            }

            messages.Insert(0, lastCommitMessage);
        }

        return messages;
    }

    /// <summary>
    ///  The menu text of a message: its first line, shortened to upstream's 72 characters.
    /// </summary>
    public static string Label(string message)
    {
        int newline = message.IndexOf('\n');
        string label = (newline == -1 ? message : message[..newline]).TrimEnd('\r');
        return label.Length > MaxLabelLength ? label.ShortenTo(MaxLabelLength) : label;
    }

    /// <summary>
    ///  Upstream's "Generate a list of changes in submodules": for each staged submodule change (a path in .gitmodules whose
    ///  folder exists), the commits between the recorded and the staged revision. Empty when none of
    ///  <paramref name="stagedPaths"/> is such a submodule.
    /// </summary>
    public static Task<string> SubmoduleChangesAsync(string repositoryPath, IReadOnlyList<string> stagedPaths)
        => Task.Run(() =>
        {
            GitModule module = Module(repositoryPath);
            ISubmodulesConfigFile configFile = module.GetSubmodulesConfigFile();
            Dictionary<string, string> modules = [];
            foreach (string path in stagedPaths)
            {
                IConfigSection? section = configFile.ConfigSections
                    .FirstOrDefault(section => section.GetValue("path").Trim() == path);
                if (section?.SubSection is { } name && Directory.Exists(Path.Combine(module.WorkingDir, path))
                                                    && !modules.ContainsKey(path))
                {
                    modules.Add(path, name.Trim());
                }
            }

            if (modules.Count == 0)
            {
                return "";
            }

            StringBuilder sb = new();
            sb.AppendLine(
                "Submodule" + (modules.Count == 1 ? " " : "s ") + string.Join(", ", modules.Keys) + " updated");
            sb.AppendLine();
            foreach ((string path, string name) in modules)
            {
                GitArgumentBuilder args = new("diff")
                {
                    "--no-ext-diff",
                    "--cached",
                    "-z",
                    "--",
                    path.QuoteNE()
                };
                string[] lines = module.GitExecutable.GetOutput(args)
                    .Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries);

                // Upstream expects both lines (Single); a submodule added or removed in this commit has one, and is left out.
                string? from =
                    lines.FirstOrDefault(line => line.StartsWith("-" + SubprojectCommit, StringComparison.Ordinal))
                        ?[(SubprojectCommit.Length + 1)..];
                string? to =
                    lines.FirstOrDefault(line => line.StartsWith("+" + SubprojectCommit, StringComparison.Ordinal))
                        ?[(SubprojectCommit.Length + 1)..];
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                {
                    continue;
                }

                sb.AppendLine("Submodule " + path + ":");
                GitModule submodule = Module(Path.Combine(module.WorkingDir, path).EnsureTrailingPathSeparator());
                string log = submodule.GitExecutable.GetOutput(new GitArgumentBuilder("log")
                {
                    "--pretty=format:\"    %m %h - %s\"", "--no-merges", $"{from}...{to}".Quote()
                });
                sb.AppendLine(log.Length != 0 ? log : "    * Revision changed to " + to[..Math.Min(7, to.Length)]);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        });

    private static GitModule Module(string path) => new(new GitExecutorProvider(new GitDirectoryResolver()), path);
}
