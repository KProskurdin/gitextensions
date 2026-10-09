using System.Diagnostics;
using System.Text.RegularExpressions;
using GitCommands;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  A commit message template from the commit window's "Commit templates" menu.
/// </summary>
/// <param name="IsRegex">Upstream's regex templates: each <c>{{pattern}}[index]</c> is replaced by that group of the
///  pattern's match in the current branch name.</param>
public sealed record CommitTemplate(string Name, string Text, bool IsRegex);

/// <summary>
///  Upstream's commit templates: those plugins register (upstream's <see cref="CommitTemplateManager"/>, whose registry
///  is shared by the whole app) and the user's own (upstream's <c>CommitTemplates</c> setting), applied as upstream's
///  <c>FormCommit.ReplaceMessage</c> applies them.
/// </summary>
public static partial class CommitTemplates
{
    // The registry is static upstream; the module is only needed for git's commit.template, which is not read here.
    private static readonly CommitTemplateManager _manager = new(() => GitModules.Open(""));

    [GeneratedRegex(@"\{\{(?<pattern>.*?)\}\}(?:\[(?<index>\d+)\])?", RegexOptions.ExplicitCapture)]
    private static partial Regex ReplaceMessageRegex { get; }

    /// <summary>
    ///  Upstream's <c>GitUICommands.AddCommitTemplate</c>: a plugin's template, by its key; a key registered already is
    ///  kept as it is.
    /// </summary>
    public static void Register(string key, Func<string> text, bool isRegex = false) => _manager.Register(key, text, icon: null, isRegex);

    public static void Unregister(string key) => _manager.Unregister(key);

    /// <summary>
    ///  The plugins' templates, with their text as it is now.
    /// </summary>
    public static IReadOnlyList<CommitTemplate> Registered()
        => [.. _manager.RegisteredTemplates.Select(item => new CommitTemplate(item.Name, item.Text, item.IsRegex))];

    /// <summary>
    ///  The user's templates from upstream's settings, without the unnamed ones, as upstream's menu lists them.
    /// </summary>
    public static IReadOnlyList<CommitTemplate> FromSettings()
        => [.. (CommitTemplateItem.LoadFromSettings() ?? [])
            .Where(item => !string.IsNullOrEmpty(item.Name))
            .Select(item => new CommitTemplate(item.Name, item.Text, item.IsRegex))];

    /// <summary>
    ///  The message a template gives, as upstream's <c>ReplaceMessage</c>: a regex template's placeholders are replaced from
    ///  <paramref name="currentBranch"/> (empty when the pattern does not match); a broken pattern leaves the text as it is.
    /// </summary>
    public static string Apply(CommitTemplate template, string currentBranch)
    {
        string message = template.Text;
        if (!template.IsRegex)
        {
            return message;
        }

        try
        {
            foreach (Match placeholder in ReplaceMessageRegex.Matches(message))
            {
                int groupIndex = int.TryParse(placeholder.Groups["index"].ValueSpan, out int index) ? index : 1;
                MatchCollection matches = new Regex(placeholder.Groups["pattern"].Value).Matches(currentBranch);
                string replacement = matches.Count > 0 && matches[0].Groups.Count > groupIndex ? matches[0].Groups[groupIndex].Value : "";
                message = message.Replace(placeholder.Groups[0].Value, replacement);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"ReplaceMessage with regex replace exception: {ex}");
        }

        return message;
    }
}
