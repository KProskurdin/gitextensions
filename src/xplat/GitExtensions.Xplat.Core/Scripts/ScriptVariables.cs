using System.Globalization;
using System.Text.RegularExpressions;
using GitCommands;

namespace GitExtensions.Xplat.Core.Scripts;

/// <summary>
///  A ref on a commit, as a script variable sees it. <see cref="Name"/> is the short name ("main", "v1.0",
///  "origin/main"); <see cref="Remote"/> is set for a remote branch.
/// </summary>
public sealed record ScriptRef(string Name, bool IsTag, bool IsRemote, string Remote = "");

/// <summary>
///  A commit, as a script variable sees it.
/// </summary>
public sealed record ScriptRevision(
    string Hash,
    string Subject,
    string? Body,
    string Author,
    string Committer,
    DateTime AuthorDate,
    DateTime CommitDate,
    IReadOnlyList<ScriptRef> Refs);

/// <summary>
///  What a script's variables are taken from. The new shell implements it over the open repository and the grid.
/// </summary>
public interface IScriptContext
{
    string WorkingDir { get; }

    /// <summary>
    ///  The repository's name as the recent list shows it (upstream's <c>IRepositoryDescriptionProvider</c>).
    /// </summary>
    string RepoName { get; }

    /// <summary>
    ///  The commit selected in the grid (the last one selected), or null when there is no grid or no selection.
    /// </summary>
    ScriptRevision? Selected { get; }

    /// <summary>
    ///  All commits selected in the grid.
    /// </summary>
    IReadOnlyList<string> SelectedHashes { get; }

    /// <summary>
    ///  The commit at HEAD with its refs; the full message only when <paramref name="loadBody"/>. Null when there is none.
    /// </summary>
    Task<ScriptRevision?> GetCurrentAsync(bool loadBody);

    /// <summary>
    ///  The checked-out branch, or empty when HEAD is detached.
    /// </summary>
    string CurrentBranch { get; }

    /// <summary>
    ///  The remote the checked-out branch tracks (upstream's <c>GetCurrentRemote</c>), or empty.
    /// </summary>
    string CurrentRemote { get; }

    string GetConfig(string key);

    /// <summary>
    ///  Options the place the script runs from adds, e.g. the selected files (upstream's <c>IScriptOptionsProvider</c>).
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> ExtraOptions { get; }

    /// <summary>
    ///  Asks the user to pick one of several refs or remotes, where upstream shows its quick selector. Empty when cancelled.
    /// </summary>
    Task<string> ChooseAsync(IReadOnlyList<string> options);
}

/// <summary>
///  Replaces a script's variables (<c>{sHash}</c>, <c>{cBranch}</c>, ...) with their values: the new shell's version of
///  upstream's <c>ScriptOptionsParser</c>, which works on WinForms and <c>IGitUICommands</c>. The variables, their values and
///  the quoting are upstream's: <c>{x}</c> inserts the value, <c>{{x}}</c> the value in double quotes.
/// </summary>
public static partial class ScriptVariables
{
    private const string CurrentMessage = "cMessage";
    private const string Head = "HEAD";

    [GeneratedRegex(@"(?<!\\)""", RegexOptions.ExplicitCapture)]
    private static partial Regex QuoteRegex { get; }

    /// <summary>
    ///  Upstream's script options, in upstream's order.
    /// </summary>
    public static IReadOnlyList<string> Options { get; } =
    [
        "sHashes", "sTag", "sBranch", "sLocalBranch", "sRemoteBranch", "sRemoteBranchName", "sRemote", "sRemoteUrl",
        "sRemotePathFromUrl", "sHash", "sMessage", "sSubject", "sAuthor", "sCommitter", "sAuthorDate", "sCommitDate",
        Head,
        "cTag", "cBranch", "cLocalBranch", "cRemoteBranch", "cRemoteBranchName", "cHash", CurrentMessage, "cSubject",
        "cAuthor", "cCommitter", "cAuthorDate", "cCommitDate", "cDefaultRemote", "cDefaultRemoteUrl",
        "cDefaultRemotePathFromUrl", "RepoName", "WorkingDir",
    ];

    public static bool Contains(string arguments, string option) =>
        arguments.Contains(CreateOption(option, quoted: false));

    /// <summary>
    ///  True for the options that need a selected commit (they start with "s").
    /// </summary>
    public static bool DependsOnSelectedRevision(string option) => option.StartsWith('s');

    /// <summary>
    ///  <paramref name="arguments"/> with every option replaced; null when an option needs a commit that is not there
    ///  (upstream then aborts the script).
    /// </summary>
    public static async Task<string?> ExpandAsync(string? arguments, IScriptContext context)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return arguments;
        }

        ScriptRevision? current = null;
        string currentRemote = "";
        foreach (string option in Options)
        {
            if (!Contains(arguments, option))
            {
                continue;
            }

            if (current is null && (option.StartsWith('c') || option == Head))
            {
                current = await context.GetCurrentAsync(loadBody: Contains(arguments, CurrentMessage));
                if (current is null)
                {
                    return null;
                }

                currentRemote = await GetCurrentRemoteAsync(context, current);
            }
            else if (DependsOnSelectedRevision(option) && context.Selected is null)
            {
                return null;
            }

            string value = await ValueAsync(option, context, context.Selected, current, currentRemote);
            arguments = ReplaceOption(option, arguments, [value]);
        }

        foreach ((string option, IReadOnlyList<string> values) in context.ExtraOptions)
        {
            arguments = ReplaceOption(option, arguments, values);
        }

        return arguments;
    }

    /// <summary>
    ///  Replaces <c>{{option}}</c> with the quoted values and <c>{option}</c> with the plain values, as upstream.
    /// </summary>
    public static string ReplaceOption(string option, string arguments, IReadOnlyList<string> values)
        => arguments
            .Replace(CreateOption(option, quoted: true), string.Join(' ', values.Select(Quote)))
            .Replace(CreateOption(option, quoted: false), string.Join(' ', values));

    /// <summary>
    ///  The path part of a remote URL without ".git" (e.g. "/owner/repo"), also for scp-style URLs, as upstream.
    /// </summary>
    public static string GetRemotePath(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || Uri.TryCreate("ssh://" + url.Replace(":", "/"), UriKind.Absolute, out uri))
        {
            return uri.LocalPath.SubstringUntilLast('.');
        }

        return "";
    }

    private static async Task<string> ValueAsync(string option, IScriptContext context, ScriptRevision? selected,
        ScriptRevision? current, string currentRemote)
    {
        IReadOnlyList<ScriptRef> selectedRefs = selected?.Refs ?? [];
        IReadOnlyList<ScriptRef> currentRefs = current?.Refs ?? [];
        return option switch
        {
            "sHashes" => string.Join(" ", context.SelectedHashes),
            "sTag" => await ChooseAsync(context, Names(selectedRefs, tags: true)),
            "sBranch" => await ChooseAsync(context, Names(selectedRefs, tags: false)),
            "sLocalBranch" => await ChooseAsync(context, Names(selectedRefs, tags: false, remote: false)),
            "sRemoteBranch" => await ChooseAsync(context, Names(selectedRefs, tags: false, remote: true)),
            "sRemoteBranchName" => StripRemoteName(await ChooseAsync(context,
                Names(selectedRefs, tags: false, remote: true))),
            "sRemote" => await ChooseAsync(context, Remotes(selectedRefs)),
            "sRemoteUrl" => await RemoteUrlAsync(context, await ChooseAsync(context, Remotes(selectedRefs))),
            "sRemotePathFromUrl" => PathOfRemote(context, await ChooseAsync(context, Remotes(selectedRefs))),
            "sHash" => selected!.Hash,
            "sMessage" => EscapeLinefeeds(selected!.Body) ?? selected.Subject,
            "sSubject" => selected!.Subject,
            "sAuthor" => selected!.Author,
            "sCommitter" => selected!.Committer,
            "sAuthorDate" => selected!.AuthorDate.ToString(CultureInfo.CurrentCulture),
            "sCommitDate" => selected!.CommitDate.ToString(CultureInfo.CurrentCulture),
            Head => context.CurrentBranch.Length > 0 ? context.CurrentBranch : current!.Hash,
            "cTag" => await ChooseAsync(context, Names(currentRefs, tags: true)),
            "cBranch" => await ChooseAsync(context, Names(currentRefs, tags: false)),
            "cLocalBranch" => await ChooseAsync(context, Names(currentRefs, tags: false, remote: false)),
            "cRemoteBranch" => await ChooseAsync(context, Names(currentRefs, tags: false, remote: true)),
            "cRemoteBranchName" => StripRemoteName(await ChooseAsync(context,
                Names(currentRefs, tags: false, remote: true))),
            "cHash" => current!.Hash,
            CurrentMessage => EscapeLinefeeds(current!.Body) ?? current.Subject,
            "cSubject" => current!.Subject,
            "cAuthor" => current!.Author,
            "cCommitter" => current!.Committer,
            "cAuthorDate" => current!.AuthorDate.ToString(CultureInfo.CurrentCulture),
            "cCommitDate" => current!.CommitDate.ToString(CultureInfo.CurrentCulture),
            "cDefaultRemote" => currentRemote,
            "cDefaultRemoteUrl" => await RemoteUrlAsync(context, currentRemote),
            "cDefaultRemotePathFromUrl" => PathOfRemote(context, currentRemote),
            "RepoName" => context.RepoName,
            "WorkingDir" => context.WorkingDir,
            _ => "",
        };
    }

    // As upstream: the tracked remote of the only local branch at HEAD; otherwise the current remote, or the remote of
    // a branch the user picks.
    private static async Task<string> GetCurrentRemoteAsync(IScriptContext context, ScriptRevision current)
    {
        IReadOnlyList<string> localBranches = Names(current.Refs, tags: false, remote: false);
        if (localBranches.Count == 1)
        {
            return context.GetConfig($"branch.{localBranches[0]}.remote");
        }

        if (context.CurrentRemote.Length > 0)
        {
            return context.CurrentRemote;
        }

        string branch = await ChooseAsync(context, localBranches);
        return branch.Length == 0 ? "" : context.GetConfig($"branch.{branch}.remote");
    }

    // Upstream reads the URL only for a known remote; GetRemotePath("") would give "/".
    private static string PathOfRemote(IScriptContext context, string remote)
        => remote.Length == 0 ? "" : GetRemotePath(context.GetConfig($"remote.{remote}.url"));

    private static Task<string> RemoteUrlAsync(IScriptContext context, string remote)
        => Task.FromResult(remote.Length == 0 ? "" : context.GetConfig($"remote.{remote}.url"));

    private static async Task<string> ChooseAsync(IScriptContext context, IReadOnlyList<string> options)
        => options.Count switch
        {
            0 => "",
            1 => options[0],
            _ => await context.ChooseAsync(options),
        };

    private static IReadOnlyList<string> Names(IReadOnlyList<ScriptRef> refs, bool tags, bool? remote = null)
        => [.. refs.Where(r => r.IsTag == tags && (remote is null || r.IsRemote == remote)).Select(r => r.Name)];

    private static IReadOnlyList<string> Remotes(IReadOnlyList<ScriptRef> refs)
        => [.. refs.Where(r => r.IsRemote && !r.IsTag).Select(r => r.Remote).Distinct()];

    private static string StripRemoteName(string remoteBranch)
    {
        int slash = remoteBranch.IndexOf('/');
        return slash >= 0 ? remoteBranch[(slash + 1)..] : remoteBranch;
    }

    private static string? EscapeLinefeeds(string? text) => text?.Replace("\n", "\\n");

    private static string CreateOption(string option, bool quoted)
        => quoted ? "{{" + option.Trim() + "}}" : "{" + option.Trim() + "}";

    private static string Quote(string value)
    {
        string quoted = '"' + QuoteRegex.Replace(value, "\\\"");
        if (quoted.EndsWith('\\'))
        {
            quoted += '\\';
        }

        return quoted + '"';
    }
}
