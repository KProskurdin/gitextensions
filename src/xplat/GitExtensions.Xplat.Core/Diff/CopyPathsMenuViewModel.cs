using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The kinds of path upstream's "Copy path(s)" items copy.
/// </summary>
public enum CopyPathsKind
{
    RelativePosix,
    RelativeNative,
    FullNative,
    FullWsl,
    FullCygwin,
}

/// <summary>
///  Upstream's <c>CopyPathsToolStripMenuItem</c>: the items of a "Copy path(s)" submenu, relative paths (POSIX, native)
///  then full paths (native, WSL, Cygwin).
/// </summary>
/// <param name="paths">The repository-relative paths to copy, read when an item runs.</param>
/// <param name="repositoryPath">The working directory the full paths start from, read when an item runs.</param>
/// <param name="copy">Puts the text on the clipboard.</param>
public sealed partial class CopyPathsMenuViewModel(Func<IEnumerable<string>> paths, Func<string?> repositoryPath,
    Func<string, Task> copy, Func<string, string>? translate = null)
{
    private static readonly (string Text, CopyPathsKind Kind, bool IsBold)[] _items =
    [
        ("Copy relative path(s) - _POSIX", CopyPathsKind.RelativePosix, false),
        ("Copy relative path(s) - _native", CopyPathsKind.RelativeNative, false),
        ("Copy _full path(s) - native", CopyPathsKind.FullNative, true),
        ("Copy full path(s) - _WSL", CopyPathsKind.FullWsl, false),
        ("Copy full path(s) - _Cygwin", CopyPathsKind.FullCygwin, false),
    ];

    /// <summary>
    ///  The submenu's items.
    /// </summary>
    public IReadOnlyList<MenuEntry> Entries()
        => [.. _items.Select(item => new MenuEntry((translate ?? (text => text))(item.Text), CopyCommand, item.Kind)
        {
            HasAccessKey = true,
            IsBold = item.IsBold,
        })];

    /// <summary>
    ///  The text an item copies: the paths, one per line, without repeats.
    /// </summary>
    public static string Text(IEnumerable<string> paths, string root, CopyPathsKind kind)
        => string.Join(Environment.NewLine, paths.Distinct().Select(path => kind switch
        {
            CopyPathsKind.RelativePosix => path.ToPosixPath(),
            CopyPathsKind.RelativeNative => path.ToNativePath(),
            CopyPathsKind.FullNative => Path.Combine(root, path).ToNativePath(),
            CopyPathsKind.FullWsl => Path.Combine(root, path).ToWslPath() ?? Path.Combine(root, path),
            _ => Path.Combine(root, path).ToCygwinPath() ?? Path.Combine(root, path),
        }));

    [RelayCommand]
    private Task CopyAsync(CopyPathsKind kind)
    {
        string text = Text(paths(), repositoryPath() ?? "", kind);
        return text.Length > 0 ? copy(text) : Task.CompletedTask;
    }
}
