using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream's <c>FormFindInCommitFilesGitGrep</c> as <see cref="GitGrepViewModel"/> drives it: opened over the list's
///  window with its searches, its expression and whether the list shows its box.
/// </summary>
public interface IGitGrepWindow
{
    /// <summary>
    ///  Whether the window is open.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    ///  Shows the window, made when it is not open, and activates it.
    /// </summary>
    void Show();

    void Close();

    void SetExpression(string text);

    void SetSearchItems(IReadOnlyList<string> searches);

    void SetShowBox(bool visible);
}

/// <summary>
///  Upstream FileStatusList's "Find in commit files using git-grep" for a list that shows a commit's files
///  (<c>cboFindInCommitFilesGitGrep</c>, <c>DeleteSearchButton</c>, the toolbar's <c>btnFindInFilesGitGrep</c> and its
///  menu): the box below the list's filter while upstream's setting shows it, searched 200 ms after typing stops and at once
///  for an earlier search picked from its drop-down (the last 30, latest first); the button that shows the box, the window
///  or both as upstream's "Using ..." choice says; the menu of upstream's git-grep settings. The list reads its files
///  again with the search's git-grep argument.
/// </summary>
public sealed partial class GitGrepViewModel : ObservableObject
{
    // Upstream's SearchFilterMaxLength.
    public const int MaxSearches = 30;

    // Upstream's tsmiFindUsingOptions text, before the options in effect.
    public const string OptionsText = "Options";

    // Upstream's FindInFilesGitGrepTypeIndex choices.
    public const int UsingWindow = 0;
    public const int UsingBox = 1;
    public const int UsingBoth = 2;

    // Upstream's "Options" submenu: git-grep's pattern kinds.
    private static readonly string[] _patternOptions =
        ["--basic-regexp", "--extended-regexp", "--fixed-strings", "--perl-regexp"];

    private readonly IAppPreferences _preferences;
    private readonly Action<string> _search;
    private readonly Func<string, string> _translate;
    private CancellationTokenSource? _typing;

    // True while the view model sets the box's text or list itself: the box's change is then not the user's.
    private bool _settingText;

    /// <param name="search">Reads the list again with a git-grep argument ("" for none).</param>
    /// <param name="window">Makes the window for this view model.</param>
    public GitGrepViewModel(IAppPreferences preferences, Action<string> search,
        Func<GitGrepViewModel, IGitGrepWindow> window, Func<string, string>? translate = null)
    {
        _preferences = preferences;
        _search = search;
        _translate = translate ?? (text => text);
        Window = window(this);
        IsBoxVisible = preferences.ShowFindInCommitFilesGitGrep;
        PatternOptionEntries = [.. _patternOptions.Select(option => new MenuEntry(option, SetPatternOptionCommand, option))];
        RefreshMenu();
    }

    /// <summary>
    ///  Raised when the box is shown, which then has the focus.
    /// </summary>
    public event EventHandler? BoxFocusRequested;

    /// <summary>
    ///  The typing delay of upstream's <c>cboFindInCommitFilesGitGrep_TextUpdate</c>; tests shorten it.
    /// </summary>
    public TimeSpan TypingDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    public IGitGrepWindow Window { get; }

    /// <summary>
    ///  The earlier searches, latest first: the box's drop-down.
    /// </summary>
    public ObservableCollection<string> Searches { get; } = [];

    /// <summary>
    ///  The search in effect.
    /// </summary>
    public string Searched { get; private set; } = "";

    /// <summary>
    ///  Completes when the search asked for last has been handed to the list, for tests.
    /// </summary>
    public Task SearchCompleted { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  The box's text; a change the user made is searched after the typing delay.
    /// </summary>
    [ObservableProperty]
    public partial string? BoxText { get; set; } = "";

    /// <summary>
    ///  An earlier search picked in the box's drop-down, searched at once.
    /// </summary>
    [ObservableProperty]
    public partial string? PickedSearch { get; set; }

    [ObservableProperty]
    public partial bool IsBoxVisible { get; private set; }

    [ObservableProperty]
    public partial bool IsDeleteVisible { get; private set; }

    [ObservableProperty]
    public partial bool MatchCase { get; private set; }

    [ObservableProperty]
    public partial bool MatchWholeWord { get; private set; }

    [ObservableProperty]
    public partial string OptionsHeader { get; private set; } = "";

    [ObservableProperty]
    public partial bool UsesWindow { get; private set; }

    [ObservableProperty]
    public partial bool UsesBox { get; private set; }

    [ObservableProperty]
    public partial bool UsesBoth { get; private set; }

    public IReadOnlyList<MenuEntry> PatternOptionEntries { get; }

    /// <summary>
    ///  Upstream's <c>FindInFilesGitGrep_DropDownOpening</c> and <c>UpdateToolbar</c>: the menu shows the settings, which
    ///  the other lists may have changed.
    /// </summary>
    public void RefreshMenu()
    {
        MatchCase = !_preferences.GitGrepIgnoreCase;
        MatchWholeWord = _preferences.GitGrepMatchWholeWord;
        OptionsHeader = $"{_translate(OptionsText)}: {_preferences.GitGrepUserArguments}";
        int findUsing = _preferences.FindInFilesGitGrepTypeIndex;
        (UsesWindow, UsesBox, UsesBoth) = (findUsing == UsingWindow, findUsing == UsingBox, findUsing == UsingBoth);
    }

    /// <summary>
    ///  Upstream's <c>SetFindInCommitFilesGitGrepVisibility</c>: the box shown and focused, or hidden; a hidden box with no
    ///  window to keep its search clears it.
    /// </summary>
    public void SetBoxVisible(bool visible)
    {
        if (Window.IsOpen)
        {
            Window.SetShowBox(visible);
        }

        if (IsBoxVisible == visible)
        {
            return;
        }

        IsBoxVisible = visible;
        if (visible)
        {
            BoxFocusRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (!Window.IsOpen && (BoxText ?? "").Length > 0)
        {
            SetText("");
            Search("", TimeSpan.Zero);
        }
    }

    /// <summary>
    ///  Upstream's <c>ShowFindInCommitFileGitGrepDialog</c>: the window with <paramref name="text"/> (else the box's search)
    ///  and the earlier searches.
    /// </summary>
    public void ShowWindow(string text)
    {
        Window.Show();
        if (text.Length > 0)
        {
            Window.SetExpression(text);
        }
        else if (Searched.Length > 0)
        {
            Window.SetExpression(Searched);
        }

        Window.SetSearchItems(Searches);
        Window.SetShowBox(IsBoxVisible);
    }

    /// <summary>
    ///  The window's Find: searches at once and puts the search in the box.
    /// </summary>
    public void SearchFromWindow(string search)
    {
        Search(search, TimeSpan.Zero);
        SetText(search);
    }

    /// <summary>
    ///  The window's check box that shows the list's box.
    /// </summary>
    public void SetBoxVisibleFromWindow(bool visible)
    {
        _preferences.ShowFindInCommitFilesGitGrep = visible;
        SetBoxVisible(visible);
    }

    /// <summary>
    ///  Upstream's <c>FindInFilesGitGrep_ButtonClick</c>: the button toggles the box, the window or both as the "Using ..."
    ///  choice says; a choice in its menu shows them.
    /// </summary>
    public void Toggle(bool fromButton)
    {
        int findUsing = _preferences.FindInFilesGitGrepTypeIndex;
        bool usingInputBox = findUsing != UsingWindow;
        bool usingWindow = findUsing != UsingBox;
        bool isVisible = (!usingInputBox || IsBoxVisible) && (!usingWindow || Window.IsOpen);
        bool setVisible = !fromButton || !isVisible;
        bool boxVisible = setVisible && usingInputBox;
        _preferences.ShowFindInCommitFilesGitGrep = boxVisible;
        _preferences.Save();
        SetBoxVisible(boxVisible);
        if (setVisible && usingWindow)
        {
            ShowWindow("");
        }
        else if (Window.IsOpen)
        {
            Window.Close();
        }
    }

    /// <summary>
    ///  Searches again with the search in effect, after a git-grep setting changed.
    /// </summary>
    public void SearchAgain() => Search(Searched, TimeSpan.Zero);

    partial void OnBoxTextChanged(string? value)
    {
        if (!_settingText)
        {
            Search(value ?? "", TypingDelay);
        }
    }

    partial void OnPickedSearchChanged(string? value)
    {
        if (!_settingText && value is { } picked)
        {
            Search(picked, TimeSpan.Zero);
        }
    }

    // Upstream's DeleteSearchButton.
    [RelayCommand]
    private void Clear()
    {
        SetText("");
        Search("", TimeSpan.Zero);
    }

    // The toolbar's split button.
    [RelayCommand]
    private void ToggleFromButton() => Toggle(fromButton: true);

    [RelayCommand]
    private void ToggleMatchCase() => ChangeSetting(() => _preferences.GitGrepIgnoreCase = !_preferences.GitGrepIgnoreCase);

    [RelayCommand]
    private void ToggleMatchWholeWord()
        => ChangeSetting(() => _preferences.GitGrepMatchWholeWord = !_preferences.GitGrepMatchWholeWord);

    [RelayCommand]
    private void SetPatternOption(string option) => ChangeSetting(() => _preferences.GitGrepUserArguments = option);

    // Upstream's "Using dialog", "Using input box" and "Using both": stored, then shown.
    [RelayCommand]
    private void SetFindUsing(int choice)
    {
        _preferences.FindInFilesGitGrepTypeIndex = choice;
        _preferences.Save();
        RefreshMenu();
        Toggle(fromButton: false);
    }

    private void ChangeSetting(Action change)
    {
        change();
        _preferences.Save();
        RefreshMenu();
        SearchAgain();
    }

    // Upstream's FindInCommitFilesGitGrep: after the delay the list is read again with the search's argument, and a search
    // goes first in the drop-down.
    private void Search(string search, TimeSpan delay)
    {
        IsDeleteVisible = search.Length > 0;
        _typing?.Cancel();
        CancellationTokenSource typing = new();
        _typing = typing;
        SearchCompleted = RunAsync();
        return;

        async Task RunAsync()
        {
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, typing.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            Searched = search;
            _search(GitGrep.SearchArgument(search));
            if (search.Length > 0)
            {
                Remember(search);
            }
        }
    }

    private void Remember(string search)
    {
        int index = Searches.IndexOf(search);
        if (index == 0)
        {
            return;
        }

        _settingText = true;
        try
        {
            if (index > 0)
            {
                Searches.RemoveAt(index);
            }
            else if (Searches.Count >= MaxSearches)
            {
                Searches.RemoveAt(MaxSearches - 1);
            }

            Searches.Insert(0, search);
            BoxText = search;
        }
        finally
        {
            _settingText = false;
        }

        if (Window.IsOpen)
        {
            Window.SetSearchItems(Searches);
            if (IsBoxVisible)
            {
                Window.SetExpression(search);
            }
        }
    }

    private void SetText(string text)
    {
        _settingText = true;
        try
        {
            BoxText = text;
        }
        finally
        {
            _settingText = false;
        }
    }
}
