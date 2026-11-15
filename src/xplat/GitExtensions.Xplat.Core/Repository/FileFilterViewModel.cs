using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitCommands;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  How a file filter box is colored: as the window, green while it filters, red for an invalid expression.
/// </summary>
public enum FileFilterState
{
    None,
    Valid,
    Invalid,
}

/// <summary>
///  Upstream FileStatusList's file filter (<c>cboFilterComboBox</c>, <c>DeleteFilterButton</c>) without a control (PLAN.md
///  principle 6): a regular expression, case ignored, applied 250 ms after typing stops and at once when picked from the
///  drop-down (two samples and the last filters that matched files, 10 at most) or deleted; a full path in the working
///  directory is cut to the path in it. An invalid expression filters nothing and shows its error.
/// </summary>
public sealed partial class FileFilterViewModel : ObservableObject
{
    // Upstream's texts, samples and limits.
    public const string Watermark = "Filter files using a regular expression...";
    public const string ErrorTitle = "RegEx";
    public const int MaxFilters = 10;
    private static readonly string[] _samples = ["^(?!.*NotThisWord)", @"^(?!.*\bg?tests?/)"];

    private readonly IAppPreferences _preferences;
    private readonly Func<string, string> _translate;
    private CancellationTokenSource? _typing;
    private string _text = "";

    public FileFilterViewModel(IAppPreferences preferences, Func<string, string>? translate = null)
    {
        _preferences = preferences;
        _translate = translate ?? (text => text);
        Placeholder = _translate(Watermark);
    }

    /// <summary>
    ///  Raised when the filter in effect changed.
    /// </summary>
    public event EventHandler? FilterChanged;

    /// <summary>
    ///  Upstream's 250 ms between the last key typed and the filter.
    /// </summary>
    public TimeSpan TypingDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    ///  The working directory, whose full paths typed in the box are cut to the paths in it.
    /// </summary>
    public Func<string?> WorkingDirectory { get; set; } = () => null;

    public string Placeholder { get; }

    /// <summary>
    ///  The drop-down: the samples and the filters that matched files, newest first.
    /// </summary>
    public ObservableCollection<string> Filters { get; } = [.. _samples];

    /// <summary>
    ///  True while this changes the box's text or list itself; a change the box reports meanwhile is not the user's.
    /// </summary>
    public bool IsSettingText { get; private set; }

    /// <summary>
    ///  The text as the user types it (bound to the box): the filter follows once the typing stops (upstream's
    ///  <c>FilterComboBox_TextUpdate</c>). <see cref="SetFilter"/> applies a text at once.
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (IsSettingText)
            {
                return;
            }

            value ??= "";
            string text = Relative(value) ?? value;
            if (text == _text && text == value)
            {
                return;
            }

            _text = text;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasText));
            Run(ApplyAfterTypingAsync);
        }
    }

    /// <summary>
    ///  Upstream's <c>IsFilterActive</c>: the box has text, which its delete button clears.
    /// </summary>
    public bool HasText => _text.Length > 0;

    /// <summary>
    ///  The filter in effect; null for none (also for an invalid expression).
    /// </summary>
    [ObservableProperty]
    public partial Regex? Filter { get; private set; }

    /// <summary>
    ///  The error of an invalid expression; null for a valid one.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ErrorTip))]
    public partial string? Error { get; private set; }

    /// <summary>
    ///  The box's tooltip: upstream's "RegEx" and the error.
    /// </summary>
    public string? ErrorTip => Error is null ? null : $"{_translate(ErrorTitle)}: {Error}";

    [ObservableProperty]
    public partial FileFilterState State { get; private set; }

    /// <summary>
    ///  Upstream's <c>SetFilter</c>: <paramref name="text"/> applies at once.
    /// </summary>
    public void SetFilter(string text)
    {
        StopTyping();
        SetTextQuietly(text);
        Apply();
    }

    /// <summary>
    ///  A filter picked from the drop-down applies at once.
    /// </summary>
    public void ApplyNow()
    {
        StopTyping();
        Apply();
    }

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
        if (_preferences.TruncatePathMethod == TruncatePathMethod.FileNameOnly)
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
        string text = _text;
        if (fileCount == 0 || Filter is null || Filters.Contains(text))
        {
            return;
        }

        // The box may change its text as its list changes; the text stays the user's.
        IsSettingText = true;
        try
        {
            if (Filters.Count == MaxFilters)
            {
                Filters.RemoveAt(MaxFilters - 1);
            }

            Filters.Insert(0, text);
            OnPropertyChanged(nameof(Text));
        }
        finally
        {
            IsSettingText = false;
        }
    }

    [RelayCommand]
    private void Delete() => SetFilter("");

    private void SetTextQuietly(string text)
    {
        IsSettingText = true;
        try
        {
            _text = text;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(HasText));
        }
        finally
        {
            IsSettingText = false;
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
            await Task.Delay(TypingDelay, typing.Token);
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
        Filter = FileStatusList.Filter(_text, out string? error);
        Error = error;
        State = _text.Length == 0 ? FileFilterState.None
            : error is null ? FileFilterState.Valid
            : FileFilterState.Invalid;
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

    // An error of the throttle shows as the box's error, as upstream's tooltip.
    private void Run(Func<Task> action) => _ = GuardAsync(action);

    private async Task GuardAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Error = ex.Message;
        }
    }
}
