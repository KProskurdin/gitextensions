using Avalonia.Controls;
using Avalonia.Input;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormFindInCommitFilesGitGrep</c>: a window beside a file list that searches the list's commit with
///  git-grep. Find searches with the expression and keeps the window open; the options, match case and whole word are
///  upstream's settings, which its git-grep commands read; the check box shows or hides the list's own git-grep box. When
///  the window closes with no expression, or with the list's box hidden, the search is cleared.
/// </summary>
public partial class FindInCommitFilesGitGrepWindow : Window
{
    private readonly IAppPreferences _preferences;
    private readonly Action<string> _locate;
    private readonly Action<bool> _toggleBox;
    private bool _loaded;
    private bool _settingShowBox;

    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public FindInCommitFilesGitGrepWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="locate">Searches the list with an expression ("" clears the search).</param>
    /// <param name="toggleBox">Shows or hides the list's git-grep box.</param>
    public FindInCommitFilesGitGrepWindow(IAppPreferences preferences, Action<string> locate, Action<bool> toggleBox)
    {
        _preferences = preferences;
        _locate = locate;
        _toggleBox = toggleBox;
        InitializeComponent();
        UpstreamTranslation.Apply(this, "FormFindInCommitFilesGitGrep");
        DialogHelp.Attach(this, HelpButton, DialogHelp.Manual("browse_repository", "diff"));
        btnSearch.Click += (_, _) => _locate(GitGrepExpressionText);
        txtOptions.TextChanged += (_, _) => Save(() => _preferences.GitGrepUserArguments = txtOptions.Text ?? "");
        chkMatchCase.IsCheckedChanged += (_, _) => Save(() => _preferences.GitGrepIgnoreCase = chkMatchCase.IsChecked != true);
        chkMatchWholeWord.IsCheckedChanged += (_, _) =>
            Save(() => _preferences.GitGrepMatchWholeWord = chkMatchWholeWord.IsChecked == true);
        chkShowSearchBox.IsCheckedChanged += (_, _) =>
        {
            bool show = chkShowSearchBox.IsChecked == true;
            Save(() => _preferences.ShowFindInCommitFilesGitGrep = show);
            if (!_settingShowBox)
            {
                _toggleBox(show);
            }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };

        // Upstream's FormClosing: no expression, or no box to keep it in, clears the search.
        Closing += (_, _) =>
        {
            if (GitGrepExpressionText.Length == 0 || chkShowSearchBox.IsChecked != true)
            {
                _locate("");
            }
        };
        Opened += (_, _) =>
        {
            _loaded = false;
            txtOptions.Text = _preferences.GitGrepUserArguments;
            chkMatchCase.IsChecked = !_preferences.GitGrepIgnoreCase;
            chkMatchWholeWord.IsChecked = _preferences.GitGrepMatchWholeWord;
            cboFindInCommitFilesGitGrep.Focus();
            _loaded = true;
        };
    }

    /// <summary>
    ///  The git-grep expression.
    /// </summary>
    public string GitGrepExpressionText
    {
        get => cboFindInCommitFilesGitGrep.Text ?? "";
        set => cboFindInCommitFilesGitGrep.Text = value;
    }

    /// <summary>
    ///  Upstream's <c>SetSearchItems</c>: the list's earlier searches in the drop-down, the expression kept.
    /// </summary>
    public void SetSearchItems(IEnumerable<string> items)
    {
        string text = GitGrepExpressionText;
        cboFindInCommitFilesGitGrep.ItemsSource = items.ToList();
        GitGrepExpressionText = text;
    }

    /// <summary>
    ///  Upstream's <c>SetShowFindInCommitFilesGitGrep</c>: the check box follows the list's box.
    /// </summary>
    public void SetShowFindInCommitFilesGitGrep(bool visible)
    {
        _settingShowBox = true;
        try
        {
            chkShowSearchBox.IsChecked = visible;
        }
        finally
        {
            _settingShowBox = false;
        }
    }

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
