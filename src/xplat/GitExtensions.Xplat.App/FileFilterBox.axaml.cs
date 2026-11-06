using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream FileStatusList's file filter (<c>cboFilterComboBox</c>, <c>DeleteFilterButton</c>): a regular expression,
///  case ignored, applied 250 ms after typing stops and at once when picked from the drop-down (two samples and the last
///  filters that matched files, 10 at most) or deleted; a full path in the working directory is cut to the path in it. The
///  box is green while it filters and red with the error as its tooltip when the expression is invalid, which filters
///  nothing.
/// </summary>
public partial class FileFilterBox : UserControl
{
    // Upstream's texts, samples and limits.
    private const string Watermark = "Filter files using a regular expression...";
    private const string ErrorTitle = "RegEx";
    private const int MaxFilters = 10;
    private static readonly TimeSpan _typingDelay = TimeSpan.FromMilliseconds(250);
    private static readonly string[] _samples = ["^(?!.*NotThisWord)", @"^(?!.*\bg?tests?/)"];

    private readonly ObservableCollection<string> _filters = [.. _samples];
    private CancellationTokenSource? _typing;
    private bool _settingText;

    public FileFilterBox()
    {
        InitializeComponent();
        cboFilterComboBox.PlaceholderText = UpstreamTranslation.ByText(Watermark);
        cboFilterComboBox.ItemsSource = _filters;
        cboFilterComboBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != ComboBox.TextProperty || _settingText)
            {
                return;
            }

            // Upstream's FilterComboBox_TextUpdate: the delete button at once, the filter after the typing stops.
            DeleteFilterButton.IsVisible = Text.Length > 0;
            if (Relative(Text) is { } relative)
            {
                SetText(relative);
            }

            UiActions.Run(ApplyAfterTypingAsync, ex => ToolTip.SetTip(cboFilterComboBox, ex.Message));
        };
        cboFilterComboBox.SelectionChanged += (_, _) =>
        {
            if (!_settingText && cboFilterComboBox.SelectedItem is string)
            {
                StopTyping();
                Apply();
            }
        };
        DeleteFilterButton.Click += (_, _) => Text = "";
    }

    /// <summary>
    ///  Raised when the filter in effect changed.
    /// </summary>
    public event EventHandler? FilterChanged;

    /// <summary>
    ///  The filter's text; set, it applies at once (upstream's <c>SetFilter</c>).
    /// </summary>
    public string Text
    {
        get => cboFilterComboBox.Text ?? "";
        set
        {
            StopTyping();
            SetText(value);
            Apply();
        }
    }

    /// <summary>
    ///  The working directory, whose full paths typed in the box are cut to the paths in it.
    /// </summary>
    public Func<string?> WorkingDirectory { get; set; } = () => null;

    /// <summary>
    ///  The filter in effect; null for none (also for an invalid expression).
    /// </summary>
    public Regex? Filter { get; private set; }

    /// <summary>
    ///  The error of an invalid expression, shown as the box's tooltip; null for a valid one.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    ///  Upstream's <c>IsFilterActive</c>: the box has text.
    /// </summary>
    public bool IsActive => Text.Length > 0;

    /// <summary>
    ///  Upstream's <c>IsFilterMatch</c> for the path: the file's path or its path before a rename, only their file names
    ///  while "Truncate long filenames" shows file names only.
    /// </summary>
    public bool IsMatch(string path, string? oldPath = null)
    {
        if (Filter is not { } filter)
        {
            return true;
        }

        string name = path.TrimEnd('/');
        if (AppServices.Preferences.TruncatePathMethod == TruncatePathMethod.FileNameOnly)
        {
            name = Path.GetFileName(name);
            oldPath = oldPath is null ? null : Path.GetFileName(oldPath);
        }

        return filter.IsMatch(name) || (oldPath is not null && filter.IsMatch(oldPath));
    }

    /// <summary>
    ///  Upstream's <c>AddToSelectionFilter</c>: after the list was filtered, a filter that left files shown goes first in the
    ///  drop-down.
    /// </summary>
    public void Shown(int fileCount)
    {
        string text = Text;
        if (fileCount == 0 || Filter is null || _filters.Contains(text))
        {
            return;
        }

        _settingText = true;
        try
        {
            if (_filters.Count == MaxFilters)
            {
                _filters.RemoveAt(MaxFilters - 1);
            }

            _filters.Insert(0, text);
            cboFilterComboBox.Text = text;
        }
        finally
        {
            _settingText = false;
        }
    }

    // Upstream's 250 ms throttle of the typed filter.
    private async Task ApplyAfterTypingAsync()
    {
        StopTyping();
        CancellationTokenSource typing = new();
        _typing = typing;
        try
        {
            await Task.Delay(_typingDelay, typing.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_typing == typing)
        {
            _typing = null;
            Apply();
        }
    }

    private void StopTyping()
    {
        _typing?.Cancel();
        _typing = null;
    }

    // Upstream's StoreFilter and its colors, then the list is filtered again.
    private void Apply()
    {
        string text = Text;
        DeleteFilterButton.IsVisible = text.Length > 0;
        Filter = FileStatusList.Filter(text, out string? error);
        Error = error;
        ToolTip.SetTip(cboFilterComboBox, error is null ? null : $"{UpstreamTranslation.ByText(ErrorTitle)}: {error}");
        if (text.Length == 0)
        {
            cboFilterComboBox.ClearValue(BackgroundProperty);
        }
        else
        {
            System.Drawing.Color color = (error is null
                ? System.Drawing.Color.FromArgb(0xC8, 0xFF, 0xC8)
                : System.Drawing.Color.FromArgb(0xFF, 0xC8, 0xC8)).AdaptBackColor();
            cboFilterComboBox.Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        }

        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    // Upstream's FilterComboBox_TextUpdate: a full path in the working directory, as the path in it.
    private string? Relative(string text)
    {
        if (WorkingDirectory() is not { } workingDir)
        {
            return null;
        }

        string posixDir = workingDir.Replace('\\', '/').TrimEnd('/') + "/";
        string posixText = text.Replace('\\', '/');
        return posixText.Length > posixDir.Length && posixText.StartsWith(posixDir, StringComparison.InvariantCultureIgnoreCase)
            ? posixText[posixDir.Length..]
            : null;
    }

    private void SetText(string text)
    {
        _settingText = true;
        try
        {
            cboFilterComboBox.Text = text;
        }
        finally
        {
            _settingText = false;
        }
    }
}
