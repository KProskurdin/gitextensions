namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  What in upstream's command line (<c>GitUICommands.RunCommandBasedOnArgument</c>) starts its clone dialog: the
///  <c>clone</c> verb, or a URL as the first argument, which is what a protocol handler gives.
/// </summary>
public static class CloneArguments
{
    private const string CloneVerb = "clone";

    private static readonly string[] _urlPrefixes = ["git://", "http://", "https://"];

    // Upstream strips these down to the URL they carry.
    private static readonly string[] _openRepoPrefixes = ["github-windows://openRepo/", "github-mac://openRepo/"];

    /// <summary>
    ///  True when <paramref name="args"/> ask for the clone dialog.
    /// </summary>
    public static bool AsksForClone(IReadOnlyList<string> args)
        => args is [CloneVerb, ..] || (args.Count > 0 && (IsUrl(args[0]) || IsOpenRepo(args[0])));

    /// <summary>
    ///  The URL the clone dialog starts with, or null when <paramref name="args"/> give none.
    /// </summary>
    public static string? Url(IReadOnlyList<string> args)
    {
        if (args is [CloneVerb, ..])
        {
            return args.Count > 1 ? args[1] : null;
        }

        if (args.Count == 0)
        {
            return null;
        }

        string first = args[0];
        return _openRepoPrefixes.FirstOrDefault(prefix => first.StartsWith(prefix, StringComparison.Ordinal)) is { } openRepo
            ? first[openRepo.Length..]
            : IsUrl(first) ? first : null;
    }

    private static bool IsUrl(string argument) => _urlPrefixes.Any(prefix => argument.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsOpenRepo(string argument) => _openRepoPrefixes.Any(prefix => argument.StartsWith(prefix, StringComparison.Ordinal));
}
