using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  The view model of upstream's <c>FormFindInCommitFilesGitGrep</c>: a window beside a file list that searches the list's
///  commit with git-grep. Find searches with the expression and keeps the window open; the options, match case and whole
///  word are upstream's settings, which its git-grep commands read, written as they change once <see cref="Load"/> read
///  them; the check box shows or hides the list's own git-grep box.
/// </summary>
public sealed partial class FindInCommitFilesGitGrepViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormFindInCommitFilesGitGrep";

    private readonly IAppPreferences _preferences;
    private readonly Action<string> _locate;
    private readonly Action<bool> _toggleBox;
    private bool _loaded;
    private bool _settingShowBox;

    /// <param name="preferences">Where upstream's git-grep settings are kept.</param>
    /// <param name="locate">Searches the list with an expression ("" clears the search).</param>
    /// <param name="toggleBox">Shows or hides the list's git-grep box.</param>
    public FindInCommitFilesGitGrepViewModel(IDialogHost host, IAppPreferences preferences, Action<string> locate,
        Action<bool> toggleBox)
        : base(host)
    {
        _preferences = preferences;
        _locate = locate;
        _toggleBox = toggleBox;
    }

    /// <summary>
    ///  The git-grep expression.
    /// </summary>
    [ObservableProperty]
    public partial string? Expression { get; set; }

    /// <summary>
    ///  The list's earlier searches.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> SearchItems { get; private set; } = [];

    /// <summary>
    ///  Upstream's <c>GitGrepUserArguments</c>.
    /// </summary>
    [ObservableProperty]
    public partial string? Options { get; set; }

    /// <summary>
    ///  The inverse of upstream's <c>GitGrepIgnoreCase</c>.
    /// </summary>
    [ObservableProperty]
    public partial bool MatchCase { get; set; }

    /// <summary>
    ///  Upstream's <c>GitGrepMatchWholeWord</c>.
    /// </summary>
    [ObservableProperty]
    public partial bool MatchWholeWord { get; set; }

    /// <summary>
    ///  Upstream's <c>ShowFindInCommitFilesGitGrep</c>: the list's own git-grep box shows.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowSearchBox { get; set; }

    /// <summary>
    ///  Reads upstream's settings; the window calls it each time it opens, and changes are written from then on.
    /// </summary>
    public void Load()
    {
        _loaded = false;
        Options = _preferences.GitGrepUserArguments;
        MatchCase = !_preferences.GitGrepIgnoreCase;
        MatchWholeWord = _preferences.GitGrepMatchWholeWord;
        _loaded = true;
    }

    /// <summary>
    ///  Upstream's <c>SetSearchItems</c>: the list's earlier searches in the drop-down, the expression kept.
    /// </summary>
    public void SetSearchItems(IEnumerable<string> items)
    {
        string? text = Expression;
        SearchItems = [.. items];
        Expression = text;
    }

    /// <summary>
    ///  Upstream's <c>SetShowFindInCommitFilesGitGrep</c>: the check box follows the list's box.
    /// </summary>
    public void SetShowSearchBox(bool visible)
    {
        _settingShowBox = true;
        try
        {
            ShowSearchBox = visible;
        }
        finally
        {
            _settingShowBox = false;
        }
    }

    /// <summary>
    ///  Upstream's <c>FormClosing</c>: no expression, or no box to keep it in, clears the search.
    /// </summary>
    public void OnClosing()
    {
        if (string.IsNullOrEmpty(Expression) || !ShowSearchBox)
        {
            _locate("");
        }
    }

    partial void OnOptionsChanged(string? value) => Save(() => _preferences.GitGrepUserArguments = value ?? "");

    partial void OnMatchCaseChanged(bool value) => Save(() => _preferences.GitGrepIgnoreCase = !value);

    partial void OnMatchWholeWordChanged(bool value) => Save(() => _preferences.GitGrepMatchWholeWord = value);

    partial void OnShowSearchBoxChanged(bool value)
    {
        Save(() => _preferences.ShowFindInCommitFilesGitGrep = value);
        if (!_settingShowBox)
        {
            _toggleBox(value);
        }
    }

    [RelayCommand]
    private void Find() => _locate(Expression ?? "");

    [RelayCommand]
    private void Close() => Host.Close(null);

    // Upstream writes the settings as they change, once the window has read them.
    private void Save(Action change)
    {
        if (!_loaded)
        {
            return;
        }

        change();
        _preferences.Save();
    }
}
