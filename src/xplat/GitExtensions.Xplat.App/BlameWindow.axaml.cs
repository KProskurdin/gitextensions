using System.Globalization;
using Avalonia.Controls;
using GitExtensions.Xplat.Core.CommitHistory;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Shows, for each line of a file, the commit and author that last changed it, as of a commit.
/// </summary>
public partial class BlameWindow : Window
{
    private readonly GitCommitHistory _history = new();
    private readonly IAppPreferences _preferences = AppServices.Preferences;
    private readonly string _repositoryPath;
    private readonly string _hash;
    private readonly string _filePath;
    private IReadOnlyList<BlameLine> _lines = [];

    public BlameWindow(string repositoryPath, string hash, string filePath)
    {
        _repositoryPath = repositoryPath;
        _hash = hash;
        _filePath = filePath;
        InitializeComponent();
        WindowPlacementTracker.Attach(this, "Xplat.BlameWindow");
        Title = $"Blame: {filePath}";
        Opened += (_, _) => UiActions.Run(LoadAsync, ex => ErrorText.Text = ex.Message);
        BlameList.DoubleTapped += (_, _) => ShowSelectedLineCommit();
        BlameList.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                e.Handled = true;
                ShowSelectedLineCommit();
            }
        };

        Toggle(IgnoreWhitespaceMenuItem, options => options with { IgnoreWhitespace = !options.IgnoreWhitespace });
        Toggle(DetectMoveInFileMenuItem, options => options with { DetectMoveInFile = !options.DetectMoveInFile });
        Toggle(DetectMoveInAllFilesMenuItem,
            options => options with { DetectMoveInAllFiles = !options.DetectMoveInAllFiles });
        Toggle(DisplayAuthorFirstMenuItem,
            options => options with { DisplayAuthorFirst = !options.DisplayAuthorFirst });
        Toggle(ShowAuthorMenuItem, options => options.ToggleShowAuthor());
        Toggle(ShowAuthorDateMenuItem, options => options.ToggleShowAuthorDate());
        Toggle(ShowAuthorTimeMenuItem, options => options with { ShowAuthorTime = !options.ShowAuthorTime });
        Toggle(ShowLineNumbersMenuItem, options => options with { ShowLineNumbers = !options.ShowLineNumbers });
        Toggle(ShowOriginalFilePathMenuItem,
            options => options with { ShowOriginalFilePath = !options.ShowOriginalFilePath });
        ShowMenuChecks();
    }

    // As upstream's blame menu: a choice is stored at once; a change to git's flags blames the file again.
    private void Toggle(MenuItem item, Func<BlameOptions, BlameOptions> change)
        => item.Click += (_, _) => UiActions.Run(() => ChangeOptionsAsync(change(_preferences.BlameOptions)),
            ex => ErrorText.Text = ex.Message);

    private async Task ChangeOptionsAsync(BlameOptions options)
    {
        bool blameAgain = !options.Arguments.SequenceEqual(_preferences.BlameOptions.Arguments);
        _preferences.BlameOptions = options;
        _preferences.Save();
        ShowMenuChecks();
        if (blameAgain)
        {
            await LoadAsync();
        }
        else
        {
            ShowRows();
        }
    }

    private void ShowMenuChecks()
    {
        BlameOptions options = _preferences.BlameOptions;
        IgnoreWhitespaceMenuItem.IsChecked = options.IgnoreWhitespace;
        DetectMoveInFileMenuItem.IsChecked = options.DetectMoveInFile;
        DetectMoveInAllFilesMenuItem.IsChecked = options.DetectMoveInAllFiles;
        DisplayAuthorFirstMenuItem.IsChecked = options.DisplayAuthorFirst;
        ShowAuthorMenuItem.IsChecked = options.ShowAuthor;
        ShowAuthorDateMenuItem.IsChecked = options.ShowAuthorDate;
        ShowAuthorTimeMenuItem.IsChecked = options.ShowAuthorTime;
        ShowAuthorTimeMenuItem.IsEnabled = options.ShowAuthorDate;
        ShowLineNumbersMenuItem.IsChecked = options.ShowLineNumbers;
        ShowOriginalFilePathMenuItem.IsChecked = options.ShowOriginalFilePath;
    }

    // Like upstream's blame, a line leads to the commit that last changed it: that commit's change to the file.
    // Lines not committed yet carry git's all-zero hash and have no commit to show.
    private void ShowSelectedLineCommit()
    {
        if (BlameList.SelectedItem is BlameRow { Line: var line } && line.Hash.Any(c => c != '0'))
        {
            new DiffWindow(_repositoryPath, line.Hash, _filePath, staged: false).Show(this);
        }
    }

    private async Task LoadAsync()
    {
        _lines = await _history.LoadBlameAsync(_repositoryPath, _hash, _filePath, _preferences.BlameOptions);
        ShowRows();
    }

    private void ShowRows()
        => BlameList.ItemsSource = _preferences.BlameOptions.Rows(_lines, _filePath, CultureInfo.CurrentCulture);
}
