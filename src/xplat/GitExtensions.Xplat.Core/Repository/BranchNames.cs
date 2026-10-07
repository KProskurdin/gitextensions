using GitCommands;
using GitCommands.Git;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Branch names as upstream's branch dialogs fix them up when the name box is left: with upstream's
///  <c>AutoNormaliseBranchName</c>, upstream's <see cref="GitBranchNameNormaliser"/> replaces what git does not allow in a
///  branch name by the <c>AutoNormaliseSymbol</c> ("_", "-", or nothing).
/// </summary>
public static class BranchNames
{
    /// <summary>
    ///  Upstream's normaliser symbol choices, as its Advanced settings page lists them.
    /// </summary>
    public static IReadOnlyList<(string Label, string Symbol)> Symbols { get; } =
        [("_", "_"), ("-", "-"), ("(none)", "")];

    /// <summary>
    ///  The name as upstream leaves it: unchanged when normalising is off or the text has no character a path may hold.
    /// </summary>
    public static string Normalise(string name, bool enabled, string symbol)
        => !enabled || !name.Any(PathUtil.IsValidPathChar)
            ? name
            : new GitBranchNameNormaliser().Normalise(name, new GitBranchNameOptions(symbol));
}
