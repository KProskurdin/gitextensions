using System.Text;
using GitCommands;
using GitCommands.Patches;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  One <c>git format-patch</c> run of upstream's FormFormatPatch: from the first parent (empty for a root commit) to the
///  commit, numbered from <see cref="Start"/> when the commits are formatted one by one.
/// </summary>
public sealed record FormatPatchRange(string From, string To, int? Start);

/// <summary>
///  Upstream's <c>FormFormatPatch</c> without its window: the runs its selection makes and its texts.
/// </summary>
public static class FormatPatchDialog
{
    // Upstream's texts.
    public const string CurrentBranch = "Current branch:";
    public const string NoOutputPathEntered = "You need to enter an output path.";
    public const string RevisionsNeeded = "You need to select at least one revision";
    public const string RevisionsNeededCaption = "Patch error";
    public const string PatchResultCaption = "Patch result";
    public const string FailCreatePatch = "Unable to create patch file(s)";

    /// <summary>
    ///  Upstream's <c>FormatPatch_Click</c>: one commit from its parent; two commits as the range from the older one's
    ///  parent to the newer one; more, each commit on its own, numbered from the oldest.
    /// </summary>
    /// <param name="selected">The selected commits, oldest first (upstream's <c>SortDirection.Descending</c>), with their
    ///  first parents.</param>
    public static IReadOnlyList<FormatPatchRange> Ranges(IReadOnlyList<(string Hash, string? FirstParent)> selected)
        => selected switch
        {
            [] => [],
            [var only] => [new(only.FirstParent ?? "", only.Hash, null)],
            [var oldest, var newest] => [new(oldest.FirstParent ?? "", newest.Hash, null)],
            _ => [.. selected.Select((commit, index) => new FormatPatchRange(commit.FirstParent ?? "", commit.Hash, index + 1))],
        };
}

/// <summary>
///  Upstream's <c>FormViewPatch</c> without its window: the patch file's file patches.
/// </summary>
public static class ViewPatchDialog
{
    // Upstream's texts.
    public const string PatchFileFilter = "Patch file (*.Patch)";
    public const string PatchFileFilterTitle = "Select patch file";

    /// <summary>
    ///  Upstream's <c>LoadPatchFile</c>: the file read losslessly and split into its file patches; none when it cannot be
    ///  read, as upstream ignores the error.
    /// </summary>
    public static IReadOnlyList<Patch> Read(string path, Func<Encoding> filesEncoding)
    {
        try
        {
            string text = File.ReadAllText(path, GitModule.LosslessEncoding);
            return [.. PatchProcessor.CreatePatchesFromString(text, new Lazy<Encoding>(filesEncoding))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            return [];
        }
    }
}
