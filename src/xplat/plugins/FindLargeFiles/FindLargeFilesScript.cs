using System.Text;
using GitCommands;

namespace GitExtensions.Plugins.FindLargeFiles;

/// <summary>
///  The script that removes files from the whole history, which the plugin runs with
///  <see cref="GitExtensions.Extensibility.Git.IGitUICommands.StartBatchFileProcessDialog"/>. On Windows it is upstream's
///  cmd.exe batch file, written as upstream's <c>FindLargeFilesForm.GenerateCommand</c> writes it; elsewhere it is the same
///  steps as a POSIX shell script, which the new shell runs with sh.
/// </summary>
public static class FindLargeFilesScript
{
    public static string Generate(IEnumerable<string> paths, bool windows)
        => windows ? Batch(paths) : Shell(paths);

    private static string Batch(IEnumerable<string> paths)
    {
        StringBuilder sb = new();
        sb.AppendLine($"SET gitexe=\"{AppSettings.GitCommand}\"");
        foreach (string path in paths)
        {
            sb.AppendLine($"%gitexe% filter-branch --index-filter \"git rm -r -f --cached --ignore-unmatch '{path}'\" --prune-empty -- --all");
        }

        sb.AppendLine("for /f \"usebackq\" %%a IN (`\"%gitexe% for-each-ref --format=\"%%^(refname^)\" refs/original/\"`) DO %gitexe% update-ref -d %%a");
        sb.AppendLine("%gitexe% reflog expire --expire=now --all");
        sb.AppendLine("%gitexe% gc --aggressive --prune=now");
        return sb.ToString();
    }

    // The same steps as the batch file. Every value is single-quoted for the shell, so a path may hold any character.
    private static string Shell(IEnumerable<string> paths)
    {
        StringBuilder sb = new();
        sb.Append("gitexe=").Append(Quote(AppSettings.GitCommand)).Append('\n');
        foreach (string path in paths)
        {
            string filter = $"git rm -r -f --cached --ignore-unmatch -- {Quote(path)}";
            sb.Append("\"$gitexe\" filter-branch --index-filter ").Append(Quote(filter))
                .Append(" --prune-empty -- --all\n");
        }

        sb.Append("\"$gitexe\" for-each-ref --format='%(refname)' refs/original/ | while read -r ref; do \"$gitexe\" update-ref -d \"$ref\"; done\n");
        sb.Append("\"$gitexe\" reflog expire --expire=now --all\n");
        sb.Append("\"$gitexe\" gc --aggressive --prune=now\n");
        return sb.ToString();
    }

    private static string Quote(string value) => $"'{value.Replace("'", @"'\''")}'";
}
