namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Which selected-line actions the window around a diff offers in its menu (upstream FileViewer's
///  <c>SupportLinePatching</c>): "Stage selected line(s)" for the working directory's diff, "Unstage selected line(s)"
///  for the index's, "Stage selected line(s)" for a commit's diff too (it applies the lines to the working directory and
///  the index), and "Reset selected line(s)" for all three.
/// </summary>
public enum DiffLineAction
{
    None,
    Stage,
    Unstage,

    /// <summary>
    ///  A commit's diff: staging applies the lines (upstream's cherry pick of lines), resetting undoes them.
    /// </summary>
    Apply,
}
