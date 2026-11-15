using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The diff of one file in its own window (PLAN.md principle 6): the window's title and the read it starts when it opens,
///  into the diff this owns (<see cref="Diff"/>).
/// </summary>
public sealed class DiffWindowViewModel : DialogViewModel
{
    // The short hash in the title, as the grid shows a commit.
    private const int ShortHashLength = 8;

    private readonly Func<DiffViewModel, Task> _show;

    /// <summary>
    ///  A diff that <paramref name="show"/> loads (a file between two revisions).
    /// </summary>
    public DiffWindowViewModel(IDialogHost host, DiffViewModel diff, string title, Func<DiffViewModel, Task> show)
        : base(host)
    {
        Diff = diff;
        Title = title;
        _show = show;
    }

    /// <summary>
    ///  The diff of <paramref name="filePath"/> in <paramref name="commitHash"/>, or of the working tree (staged or unstaged)
    ///  when no commit is given; titled by the commit's short hash, or "Staged" or "Unstaged", and the file.
    /// </summary>
    public DiffWindowViewModel(IDialogHost host, DiffViewModel diff, string repositoryPath, string? commitHash,
        string? filePath, bool staged)
        : this(host, diff, TitleOf(commitHash, filePath, staged),
            viewer => viewer.ShowAsync(repositoryPath, commitHash, filePath, staged,
                filePath ?? TitleOf(commitHash, filePath, staged)))
    {
    }

    public DiffViewModel Diff { get; }

    public string Title { get; }

    /// <summary>
    ///  Reads the diff; the window calls it when it opens, and an error shows over it.
    /// </summary>
    public Task LoadAsync() => GuardAsync(() => _show(Diff));

    private static string TitleOf(string? commitHash, string? filePath, bool staged)
    {
        string? name = filePath is null ? null : $": {filePath}";
        return commitHash is null
            ? $"{(staged ? "Staged" : "Unstaged")}{name}"
            : $"{commitHash[..Math.Min(ShortHashLength, commitHash.Length)]}{name}";
    }
}
