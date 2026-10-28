using GitExtensions.Extensibility;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>EnableButtons</c> of FormApplyPatch: what can run while no patch is being applied, and what once
///  <c>git am</c> stopped; <see cref="Default"/> is the button upstream marks and makes the accept button.
/// </summary>
public sealed record ApplyPatchButtons(bool Apply, bool FileBox, bool DirectoryBox, bool AddFiles, bool Resolved,
    bool Mergetool, bool Skip, bool Abort, bool UnresolvedConflicts, ApplyPatchDefault Default);

/// <summary>
///  The button upstream's FormApplyPatch highlights.
/// </summary>
public enum ApplyPatchDefault
{
    None,
    Resolved,
    Mergetool
}

/// <summary>
///  Upstream's <c>FormApplyPatch</c> and <c>FormAddFiles</c> without their windows.
/// </summary>
public static class ApplyPatchDialog
{
    // Upstream's texts.
    public const string ConflictResolved = "Conflicts resolved";
    public const string ConflictMergetool = "&Solve conflicts";
    public const string SelectPatchFileFilter = "Patch file (*.Patch)";
    public const string SelectPatchFileCaption = "Select patch file";
    public const string NoFileSelected = "Please select a patch to apply";
    public const string ApplyPatchCaption = "Apply patch";

    /// <summary>
    ///  Upstream's static <c>Skipped</c> of FormApplyPatch: the patches skipped in the <c>git am</c> in progress.
    /// </summary>
    public static List<RebasePatch> Skipped { get; } = [];

    /// <summary>
    ///  Upstream's <c>EnableButtons</c>.
    /// </summary>
    /// <param name="inPatch">Upstream's <c>InTheMiddleOfPatch</c>.</param>
    /// <param name="conflicted">Upstream's <c>InTheMiddleOfConflictedMerge</c>.</param>
    /// <param name="fileMode">"Patch file" is chosen rather than "Patch directory".</param>
    public static ApplyPatchButtons Buttons(bool inPatch, bool conflicted, bool fileMode)
        => new(
            Apply: !inPatch,
            FileBox: !inPatch && fileMode,
            DirectoryBox: !inPatch && !fileMode,
            AddFiles: inPatch,
            Resolved: inPatch && !conflicted,
            Mergetool: inPatch && conflicted,
            Skip: inPatch,
            Abort: inPatch,
            UnresolvedConflicts: conflicted,
            Default: conflicted ? ApplyPatchDefault.Mergetool : inPatch ? ApplyPatchDefault.Resolved : ApplyPatchDefault.None);

    /// <summary>
    ///  Upstream's highlight of the default button: its text between <c>&gt;</c> and <c>&lt;</c>.
    /// </summary>
    public static string Highlighted(string text) => ">" + text + "<";

    /// <summary>
    ///  Upstream's <c>IsDiffFile</c>: a raw diff (<c>git diff -p</c>) starts with "diff " or "Index: "; false on any problem.
    /// </summary>
    public static bool IsDiffFile(string path)
    {
        try
        {
            using StreamReader reader = new(path);
            string? line = reader.ReadLine();
            return line is not null && (line.StartsWith("diff ", StringComparison.Ordinal)
                                        || line.StartsWith("Index: ", StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                       or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    ///  Upstream's <c>Apply_Click</c> for a patch file: <c>git apply</c> for a raw diff, <c>git am</c> for a mail.
    /// </summary>
    public static ArgumentString FileArguments(string patchFile, bool signOff, bool ignoreWhitespace,
        Func<string?, string?> pathForGit)
        => IsDiffFile(patchFile)
            ? GitCommands.Git.Commands.ApplyDiffPatch(ignoreWhitespace, patchFile, pathForGit)
            : GitCommands.Git.Commands.ApplyMailboxPatch(signOff, ignoreWhitespace, patchFile, pathForGit);

    /// <summary>
    ///  Upstream's <c>FormAddFiles</c> command: <c>git add</c> of the filter, as a dry run for "Show files".
    /// </summary>
    public static string AddFilesArguments(string filter, bool force, bool dryRun)
        => string.Format("add{0}{1} {2}", dryRun ? " --dry-run" : "", force ? " -f" : "", filter);
}
