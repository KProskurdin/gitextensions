using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The view model of upstream's <c>FormDeleteTag</c>: the tag, and the remote to delete it from as well. Closes its window
///  with true after the delete.
/// </summary>
public sealed partial class DeleteTagViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormDeleteTag";

    private readonly Func<string, string?, Task>? _delete;

    /// <param name="tag">The tag in the box at first, or null.</param>
    /// <param name="tags">The tags (upstream's <c>GetRefs(RefsFilter.Tags)</c>).</param>
    /// <param name="remotes">The remotes upstream's remotes box lists.</param>
    /// <param name="currentRemote">The current branch's remote (upstream's <c>GetCurrentRemote</c>), or empty.</param>
    /// <param name="delete">Deletes the tag, and on the given remote too when it is not null.</param>
    public DeleteTagViewModel(IDialogHost host, string? tag, IReadOnlyList<string> tags, IReadOnlyList<string> remotes,
        string currentRemote, Func<string, string?, Task>? delete)
        : base(host)
    {
        _delete = delete;
        Tags = tags;
        Remotes = remotes;
        TagText = tag;
        SelectedRemote = remotes.FirstOrDefault(remote => remote == currentRemote);
    }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyList<string> Remotes { get; }

    [ObservableProperty]
    public partial string? TagText { get; set; }

    /// <summary>
    ///  Upstream's "Delete tag also from the following remote(s)".
    /// </summary>
    [ObservableProperty]
    public partial bool DeleteFromRemote { get; set; }

    [ObservableProperty]
    public partial string? SelectedRemote { get; set; }

    // Upstream's OkClick: the remote's tag only when the box names one.
    [RelayCommand]
    private Task DeleteAsync() => GuardAsync(async () =>
    {
        string tag = TagText ?? "";
        string? remote = DeleteFromRemote && tag.Length > 0 ? SelectedRemote ?? "" : null;
        if (_delete is not null)
        {
            await _delete(tag, remote);
        }

        Host.Close(true);
    });
}
