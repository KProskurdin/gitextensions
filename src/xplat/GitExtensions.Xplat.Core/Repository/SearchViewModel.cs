using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  The view model of upstream's <c>SearchWindow</c> with its <c>SearchControl</c>, for "Find file": while the user types,
///  the first matches show, the first one selected; <see cref="MoveDownCommand"/> and <see cref="MoveUpCommand"/> go
///  through them, <see cref="TakeCommand"/> takes the selected one and <see cref="CancelCommand"/> closes without one.
/// </summary>
public sealed partial class SearchViewModel : DialogViewModel
{
    private readonly Func<string, IEnumerable<string>> _getCandidates;
    private int _searchVersion;

    /// <param name="getCandidates">The matches for a text; called off the UI thread, as upstream's AsyncLoader does.</param>
    public SearchViewModel(IDialogHost host, Func<string, IEnumerable<string>> getCandidates)
        : base(host)
    {
        _getCandidates = getCandidates;
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    /// <summary>
    ///  The first matches of the latest text; they show while there are any.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCandidates))]
    public partial IReadOnlyList<string> Candidates { get; private set; } = [];

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    public bool HasCandidates => Candidates.Count > 0;

    /// <summary>
    ///  The match the user took; null when the window was closed without one.
    /// </summary>
    public string? SelectedItem { get; private set; }

    /// <summary>
    ///  The search of the latest text: done once its matches show.
    /// </summary>
    public Task Searched { get; private set; } = Task.CompletedTask;

    // Upstream's txtSearchBox_TextChange and SearchForCandidates: the first matches of the latest text.
    partial void OnSearchTextChanged(string value)
    {
        int version = ++_searchVersion;
        Searched = GuardAsync(async () =>
        {
            List<string> candidates = await Task.Run(() => _getCandidates(value).Take(FindFile.MaxCandidates).ToList());
            if (version != _searchVersion)
            {
                return;
            }

            Candidates = candidates;
            SelectedIndex = candidates.Count > 0 ? 0 : -1;
        });
    }

    // Upstream's txtSearchBox_KeyDown: Down and Up go round the matches.
    [RelayCommand]
    private void MoveDown()
    {
        if (Candidates.Count > 1)
        {
            SelectedIndex = (SelectedIndex + 1) % Candidates.Count;
        }
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (Candidates.Count > 1)
        {
            SelectedIndex = SelectedIndex - 1 < 0 ? Candidates.Count - 1 : SelectedIndex - 1;
        }
    }

    // Upstream's ItemSelectedFromList: the selected match, then the window closes (with none when nothing matched).
    [RelayCommand]
    private void Take()
    {
        SelectedItem = SelectedIndex >= 0 && SelectedIndex < Candidates.Count ? Candidates[SelectedIndex] : null;
        Host.Close(null);
    }

    [RelayCommand]
    private void Cancel()
    {
        SelectedItem = null;
        Host.Close(null);
    }
}
