namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  Upstream's <c>CommitKind</c>: what the commit window makes, a normal commit or one that git's autosquash folds into an
///  earlier commit.
/// </summary>
public enum CommitKind
{
    Normal,
    Fixup,
    Squash,
    Amend,
}

/// <summary>
///  Upstream's <c>FormCommit</c> rules for a fixup, squash or amend commit (<c>CommitKind</c>, <c>OnShown</c>): the message
///  it starts with and whether it can be typed over.
/// </summary>
public static class CommitKinds
{
    /// <summary>
    ///  Upstream's <c>CommitKindExtensions.GetPrefix</c>.
    /// </summary>
    public static string Prefix(CommitKind kind)
        => kind switch
        {
            CommitKind.Fixup => "fixup!",
            CommitKind.Squash => "squash!",
            CommitKind.Amend => "amend!",
            _ => "",
        };

    /// <summary>
    ///  Upstream's <c>OnShown</c> message for the commit <paramref name="subject"/> and <paramref name="fullMessage"/> are of:
    ///  the prefix and the subject (unless it already starts with the prefix), and for an amend commit then a blank line and
    ///  the commit's whole message, which git puts in place of the old one. Null for a normal commit, which starts from the
    ///  draft.
    /// </summary>
    public static string? Message(CommitKind kind, string subject, string fullMessage)
    {
        if (kind == CommitKind.Normal)
        {
            return null;
        }

        string prefix = Prefix(kind);
        string first = subject.StartsWith(prefix, StringComparison.Ordinal) ? subject : $"{prefix} {subject}";
        return kind == CommitKind.Amend ? $"{first}\n\n{fullMessage}" : first;
    }

    /// <summary>
    ///  Upstream's <c>messageCanBeChanged</c>: a fixup or squash message keeps the subject git matches it by, so it is read
    ///  only until "Modify the commit message" turns the commit into a normal one.
    /// </summary>
    public static bool MessageCanBeChanged(CommitKind kind) => kind is CommitKind.Normal or CommitKind.Amend;

    /// <summary>
    ///  Upstream's <c>OnFormClosed</c>: the message of a fixup or squash commit is not kept as the draft, as it has a
    ///  special meaning.
    /// </summary>
    public static bool KeepsDraft(CommitKind kind) => MessageCanBeChanged(kind);
}
