using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>SearchWindow</c> with its <c>SearchControl</c>, for "Find file": while the user types, the first
///  matches drop down below the text box, the first one selected; Up and Down move through them, Enter (or a double-click)
///  takes the selected one and Escape closes without one. Upstream has no translation for it (a generic form).
/// </summary>
public partial class SearchWindow : Window
{
    private readonly Func<string, IEnumerable<string>> _getCandidates;
    private int _searchVersion;

    public SearchWindow()
        : this(_ => [])
    {
    }

    /// <param name="getCandidates">The matches for a text; called off the UI thread, as upstream's AsyncLoader does.</param>
    public SearchWindow(Func<string, IEnumerable<string>> getCandidates)
    {
        _getCandidates = getCandidates;
        InitializeComponent();
        txtSearchBox.TextChanged += (_, _) => Search(txtSearchBox.Text ?? "");
        txtSearchBox.AddHandler(KeyDownEvent, OnSearchBoxKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        listBoxSearchResult.DoubleTapped += (_, _) => TakeSelected();
        InitialFocus.Attach(this, () => txtSearchBox);
    }

    /// <summary>
    ///  The match the user took; null when the window was closed without one.
    /// </summary>
    public string? SelectedItem { get; private set; }

    /// <summary>
    ///  The search of the latest text: done once its matches show.
    /// </summary>
    public Task Searched { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Shows the window over <paramref name="owner"/> and returns the match the user took.
    /// </summary>
    public async Task<string?> ShowAsync(Window owner)
    {
        await ShowDialog(owner);
        return SelectedItem;
    }

    // Upstream's txtSearchBox_TextChange and SearchForCandidates: the first matches of the latest text.
    private void Search(string text)
    {
        int version = ++_searchVersion;
        Searched = SearchAsync();
        return;

        async Task SearchAsync()
        {
            List<string> candidates = await Task.Run(() => _getCandidates(text).Take(FindFile.MaxCandidates).ToList());
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (version != _searchVersion)
                {
                    return;
                }

                listBoxSearchResult.ItemsSource = candidates;
                listBoxSearchResult.SelectedIndex = candidates.Count > 0 ? 0 : -1;
                listBoxSearchResult.IsVisible = candidates.Count > 0;
            });
        }
    }

    // Upstream's txtSearchBox_KeyDown and txtSearchBox_KeyUp.
    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        int count = listBoxSearchResult.ItemCount;
        switch (e.Key)
        {
            case Key.Down when count > 1:
                listBoxSearchResult.SelectedIndex = (listBoxSearchResult.SelectedIndex + 1) % count;
                e.Handled = true;
                break;
            case Key.Up when count > 1:
                listBoxSearchResult.SelectedIndex = listBoxSearchResult.SelectedIndex - 1 < 0
                    ? count - 1
                    : listBoxSearchResult.SelectedIndex - 1;
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = true;
                TakeSelected();
                break;
            case Key.Escape:
                e.Handled = true;
                SelectedItem = null;
                Close();
                break;
        }
    }

    // Upstream's ItemSelectedFromList: the selected match, then the window closes (with none when nothing matched).
    private void TakeSelected()
    {
        SelectedItem = listBoxSearchResult.IsVisible ? listBoxSearchResult.SelectedItem as string : null;
        Close();
    }
}
