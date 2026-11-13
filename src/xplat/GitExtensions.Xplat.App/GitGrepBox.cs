using System.Collections.ObjectModel;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.Diff;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's "Find in commit files using git-grep" (<c>cboFindInCommitFilesGitGrep</c>,
///  <c>DeleteSearchButton</c>, the toolbar's <c>btnFindInFilesGitGrep</c> and <c>FormFindInCommitFilesGitGrep</c>) for a
///  list that shows a commit's files: the box below the list's filter while upstream's setting shows it, searched 200 ms
///  after typing stops and at once for an earlier search picked from its drop-down (the last 30, latest first); the
///  button that shows the box, the window or both as upstream's "Using ..." choice says, and its menu of upstream's
///  git-grep settings; the window. The list reads its files again with the search's git-grep argument.
/// </summary>
public sealed class GitGrepBox
{
    // Upstream's SearchFilterMaxLength and the typing delay of cboFindInCommitFilesGitGrep_TextUpdate.
    private const int MaxSearches = 30;
    private const int TypingDelayMilliseconds = 200;

    // Upstream's "Options" submenu: git-grep's pattern kinds.
    private static readonly string[] _patternOptions =
        ["--basic-regexp", "--extended-regexp", "--fixed-strings", "--perl-regexp"];

    private readonly Window _owner;
    private readonly ComboBox _box;
    private readonly Button _delete;
    private readonly IAppPreferences _preferences;
    private readonly Action<string> _search;
    private readonly ObservableCollection<string> _searches = [];
    private FindInCommitFilesGitGrepWindow? _window;
    private CancellationTokenSource? _typing;
    private bool _settingText;
    private string _searched = "";

    /// <param name="search">Reads the list again with a git-grep argument ("" for none).</param>
    internal GitGrepBox(Window owner, ComboBox box, Button delete, GitGrepButton? button, IAppPreferences preferences,
        Action<string> search)
    {
        _owner = owner;
        _box = box;
        _delete = delete;
        _preferences = preferences;
        _search = search;
        _box.ItemsSource = _searches;
        _box.PlaceholderText = UpstreamTranslation.ByText(GitGrep.Watermark);
        _box.IsVisible = preferences.ShowFindInCommitFilesGitGrep;
        _delete.IsVisible = false;
        _box.PropertyChanged += (_, e) =>
        {
            if (e.Property == ComboBox.TextProperty && !_settingText)
            {
                Search(_box.Text ?? "", TypingDelayMilliseconds);
            }
        };
        _box.SelectionChanged += (_, _) =>
        {
            if (!_settingText && _box.SelectedItem is string picked)
            {
                Search(picked, delay: 0);
            }
        };
        _delete.Click += (_, _) =>
        {
            SetText("");
            Search("", delay: 0);
        };
        button?.Wire(this, preferences);
    }

    /// <summary>
    ///  The search in effect.
    /// </summary>
    public string Text => _searched;

    /// <summary>
    ///  Completes when the search asked for last has been handed to the list, for tests.
    /// </summary>
    public Task Searched { get; private set; } = Task.CompletedTask;

    /// <summary>
    ///  Whether the list shows the box.
    /// </summary>
    public bool BoxVisible => _box.IsVisible;

    /// <summary>
    ///  The window while it is open, for tests.
    /// </summary>
    public FindInCommitFilesGitGrepWindow? Window => _window is { IsVisible: true } window ? window : null;

    /// <summary>
    ///  Upstream's <c>SetFindInCommitFilesGitGrepVisibility</c>: the box shown and focused, or hidden; a hidden box with no
    ///  window to keep its search clears it.
    /// </summary>
    public void SetBoxVisible(bool visible)
    {
        _window?.SetShowFindInCommitFilesGitGrep(visible);
        if (_box.IsVisible == visible)
        {
            return;
        }

        _box.IsVisible = visible;
        if (visible)
        {
            _box.Focus();
        }
        else if (Window is null && (_box.Text ?? "").Length > 0)
        {
            SetText("");
            Search("", delay: 0);
        }
    }

    /// <summary>
    ///  Upstream's <c>ShowFindInCommitFileGitGrepDialog</c>: the window with <paramref name="text"/> (else the box's search)
    ///  and the earlier searches.
    /// </summary>
    public void ShowWindow(string text)
    {
        if (_window is not { IsVisible: true })
        {
            _window = new FindInCommitFilesGitGrepWindow(_preferences, search =>
            {
                Search(search, delay: 0);
                SetText(search);
            }, visible =>
            {
                _preferences.ShowFindInCommitFilesGitGrep = visible;
                SetBoxVisible(visible);
            });
            _window.Closed += (_, _) => _window = null;
            _window.Show(_owner);
        }

        if (text.Length > 0)
        {
            _window.GitGrepExpressionText = text;
        }
        else if (_searched.Length > 0)
        {
            _window.GitGrepExpressionText = _searched;
        }

        _window.SetSearchItems(_searches);
        _window.SetShowFindInCommitFilesGitGrep(_box.IsVisible);
        _window.Activate();
    }

    /// <summary>
    ///  Upstream's <c>FindInFilesGitGrep_ButtonClick</c>: the button toggles the box, the window or both as the "Using ..."
    ///  choice says; a choice in its menu shows them.
    /// </summary>
    public void Toggle(bool fromButton)
    {
        int findUsing = _preferences.FindInFilesGitGrepTypeIndex;
        bool usingInputBox = findUsing != 0;
        bool usingWindow = findUsing != 1;
        bool isVisible = (!usingInputBox || _box.IsVisible) && (!usingWindow || Window is not null);
        bool setVisible = !fromButton || !isVisible;
        bool boxVisible = setVisible && usingInputBox;
        _preferences.ShowFindInCommitFilesGitGrep = boxVisible;
        _preferences.Save();
        SetBoxVisible(boxVisible);
        if (setVisible && usingWindow)
        {
            ShowWindow("");
        }
        else
        {
            _window?.Close();
        }
    }

    /// <summary>
    ///  Searches again with the search in effect, after a git-grep setting changed.
    /// </summary>
    public void SearchAgain() => Search(_searched, delay: 0);

    // Upstream's FindInCommitFilesGitGrep: after the delay the list is read again with the search's argument, and a search
    // goes first in the drop-down.
    private void Search(string search, int delay)
    {
        _delete.IsVisible = search.Length > 0;
        _typing?.Cancel();
        CancellationTokenSource typing = new();
        _typing = typing;
        Searched = RunAsync();
        return;

        async Task RunAsync()
        {
            if (delay > 0)
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

            _searched = search;
            _search(GitGrep.SearchArgument(search));
            if (search.Length > 0)
            {
                Remember(search);
            }
        }
    }

    private void Remember(string search)
    {
        int index = _searches.IndexOf(search);
        if (index == 0)
        {
            return;
        }

        _settingText = true;
        try
        {
            if (index > 0)
            {
                _searches.RemoveAt(index);
            }
            else if (_searches.Count >= MaxSearches)
            {
                _searches.RemoveAt(MaxSearches - 1);
            }

            _searches.Insert(0, search);
            _box.Text = search;
        }
        finally
        {
            _settingText = false;
        }

        if (Window is { } window)
        {
            window.SetSearchItems(_searches);
            if (_box.IsVisible)
            {
                window.GitGrepExpressionText = search;
            }
        }
    }

    private void SetText(string text)
    {
        _settingText = true;
        try
        {
            _box.Text = text;
        }
        finally
        {
            _settingText = false;
        }
    }

    /// <summary>
    ///  The list toolbar's git-grep split button (upstream's <c>btnFindInFilesGitGrep</c>) and its menu.
    /// </summary>
    internal sealed record GitGrepButton(SplitButton Button, MenuItem MatchCase, MenuItem WholeWord, MenuItem Options,
        MenuItem UsingWindow, MenuItem UsingBox, MenuItem UsingBoth)
    {
        // Upstream's tsmiFindUsingOptions text, before the options in effect.
        private const string OptionsText = "Options";

        public void Wire(GitGrepBox box, IAppPreferences preferences)
        {
            Button.Click += (_, _) => box.Toggle(fromButton: true);
            MatchCase.Click += (_, _) => Change(() => preferences.GitGrepIgnoreCase = !preferences.GitGrepIgnoreCase);
            WholeWord.Click += (_, _) => Change(() => preferences.GitGrepMatchWholeWord = !preferences.GitGrepMatchWholeWord);
            Options.ItemsSource = _patternOptions.Select(option =>
            {
                MenuItem item = new() { Header = option };
                item.Click += (_, _) => Change(() => preferences.GitGrepUserArguments = option);
                return item;
            }).ToList();
            MenuItem[] usings = [UsingWindow, UsingBox, UsingBoth];
            for (int index = 0; index < usings.Length; index++)
            {
                int choice = index;
                usings[index].Click += (_, _) =>
                {
                    preferences.FindInFilesGitGrepTypeIndex = choice;
                    preferences.Save();
                    box.Toggle(fromButton: false);
                };
            }

            // Upstream's FindInFilesGitGrep_DropDownOpening and UpdateToolbar.
            if (Button.Flyout is MenuFlyout flyout)
            {
                flyout.Opening += (_, _) =>
                {
                    MatchCase.IsChecked = !preferences.GitGrepIgnoreCase;
                    WholeWord.IsChecked = preferences.GitGrepMatchWholeWord;
                    Options.Header = $"{UpstreamTranslation.ByText(OptionsText)}: {preferences.GitGrepUserArguments}";
                    for (int index = 0; index < usings.Length; index++)
                    {
                        usings[index].IsChecked = preferences.FindInFilesGitGrepTypeIndex == index;
                    }
                };
            }

            return;

            void Change(Action change)
            {
                change();
                preferences.Save();
                box.SearchAgain();
            }
        }
    }
}
