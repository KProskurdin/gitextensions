using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Upstream's <c>CommitPickerSmallControl</c>: the chosen commit's short hash, which can be typed (a hash or ref, resolved
///  by <see cref="SetCommitAsync"/> and refused with upstream's message when git does not know it) or picked in the choose
///  commit window (<see cref="PickCommand"/>), and <c>GetCommitCountString</c> from HEAD to it.
/// </summary>
public sealed partial class CommitPickerViewModel : ObservableObject
{
    // Upstream's texts.
    public const string InvalidCommitHash = "The given commit hash is not valid for this repository and was therefore discarded.";
    public const string ErrorCaption = "Error";

    private readonly Func<string, string> _translate;

    // Counts the counts asked for, so a later choice wins.
    private int _version;

    public CommitPickerViewModel(Func<string, string>? translate = null)
    {
        _translate = translate ?? (text => text);
    }

    /// <summary>
    ///  The choose commit window on the commit given (its full hash, or null): the hash picked, or null.
    /// </summary>
    public Func<string?, Task<string?>>? PickCommit { get; set; }

    /// <summary>
    ///  Shows an error message with its caption, and completes when it is closed.
    /// </summary>
    public Func<string, string, Task>? ShowError { get; set; }

    /// <summary>
    ///  The repository the commits are resolved in.
    /// </summary>
    [ObservableProperty]
    public partial IGitModule? Module { get; set; }

    /// <summary>
    ///  The chosen commit; null when none is.
    /// </summary>
    [ObservableProperty]
    public partial ObjectId? Selected { get; set; }

    /// <summary>
    ///  The box's text: the chosen commit's short hash, or what was typed.
    /// </summary>
    [ObservableProperty]
    public partial string HashText { get; set; } = "";

    /// <summary>
    ///  Upstream's <c>GetCommitCountString</c> from HEAD to the chosen commit, read after it is chosen.
    /// </summary>
    [ObservableProperty]
    public partial string CountText { get; private set; } = "";

    /// <summary>
    ///  Upstream's <c>SetSelectedCommitHash</c>: resolves <paramref name="revision"/>; an unknown one is refused (with a
    ///  message when something was typed) and the previous commit kept.
    /// </summary>
    public async Task SetCommitAsync(string? revision)
    {
        if (Module is not { } module || (revision ?? "") == (Selected?.ToShortString() ?? ""))
        {
            return;
        }

        ObjectId id = string.IsNullOrWhiteSpace(revision) ? default : await Task.Run(() => module.RevParse(revision));
        if (id.IsZero)
        {
            if (!string.IsNullOrWhiteSpace(revision) && ShowError is { } showError)
            {
                await showError(_translate(InvalidCommitHash), _translate(ErrorCaption));
            }

            HashText = Selected?.ToShortString() ?? "";
            return;
        }

        if (id == Selected)
        {
            // The same commit by another name: shown by its short hash again.
            HashText = id.ToShortString();
            return;
        }

        Selected = id;
    }

    partial void OnModuleChanged(IGitModule? value) => ShowSelected();

    partial void OnSelectedChanged(ObjectId? value) => ShowSelected();

    // Upstream's buttonPickCommit_Click: the choose commit window on the chosen commit.
    [RelayCommand]
    private async Task PickAsync()
    {
        if (Module is null || PickCommit is not { } pick)
        {
            return;
        }

        try
        {
            if (await pick(Selected?.ToString()) is { } hash)
            {
                await SetCommitAsync(hash);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // As upstream's control, a failed pick keeps the commit.
        }
    }

    // The chosen commit's short hash, and the count from HEAD to it, read off the UI thread.
    private void ShowSelected()
    {
        int version = ++_version;
        HashText = Selected?.ToShortString() ?? "";
        CountText = "";
        if (Selected is not { } id || Module is not { } module)
        {
            return;
        }

        _ = CountAsync();
        return;

        async Task CountAsync()
        {
            try
            {
                string count = await Task.Run(() =>
                {
                    ObjectId current = module.GetCurrentCheckout();
                    return current.IsZero ? "" : module.GetCommitCountString(current, id.ToString());
                });
                if (version == _version)
                {
                    CountText = count;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // No count, as upstream shows none when git cannot tell.
            }
        }
    }
}
