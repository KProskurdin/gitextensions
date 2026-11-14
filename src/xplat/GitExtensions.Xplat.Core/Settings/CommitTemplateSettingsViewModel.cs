using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;
using GitExtensions.Xplat.Core.Repository;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The view model of upstream's <c>FormCommitTemplateSettings</c>: the user's ten commit templates and the commit
///  validation settings, stored under upstream's keys on OK. Closes its window with true after OK, false after Cancel.
/// </summary>
public sealed partial class CommitTemplateSettingsViewModel : DialogViewModel
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormCommitTemplateSettings";

    private const int MaxShownCharsForName = 50;

    private readonly IAppPreferences _preferences;
    private readonly List<CommitTemplate> _templates;
    private readonly string _emptyTemplate;
    private bool _showing;

    public CommitTemplateSettingsViewModel(IDialogHost host, IAppPreferences preferences)
        : base(host)
    {
        _preferences = preferences;
        _templates = [.. CommitTemplates.Slots(preferences.CommitTemplates)];
        _emptyTemplate = Host.Translate(Category, "_emptyTemplate", "empty");

        CommitValidationOptions validation = preferences.CommitValidation;
        MaxFirstLineLength = validation.MaxFirstLineLength;
        MaxLineLength = validation.MaxLineLength;
        SecondLineMustBeEmpty = validation.SecondLineMustBeEmpty;
        IndentAfterFirstLine = validation.IndentAfterFirstLine;
        AutoWrap = validation.AutoWrap;
        ValidationRegex = validation.RegEx;

        TemplateNames = Names();
        SelectedIndex = 0;
    }

    /// <summary>
    ///  Upstream's <c>RefreshLineInListBox</c> lines: "1 : name", or "1 : &lt;empty&gt;" for a template without a name.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> TemplateNames { get; private set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string TemplateName { get; set; } = "";

    [ObservableProperty]
    public partial string TemplateText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsRegex { get; set; }

    [ObservableProperty]
    public partial decimal? MaxFirstLineLength { get; set; }

    [ObservableProperty]
    public partial decimal? MaxLineLength { get; set; }

    [ObservableProperty]
    public partial bool SecondLineMustBeEmpty { get; set; }

    [ObservableProperty]
    public partial bool IndentAfterFirstLine { get; set; }

    [ObservableProperty]
    public partial bool AutoWrap { get; set; }

    [ObservableProperty]
    public partial string? ValidationRegex { get; set; }

    // Upstream's _commitTemplates_SelectedIndexChanged: the template's fields. A list whose items were replaced passes no
    // selection on the way; that is ignored.
    partial void OnSelectedIndexChanged(int value)
    {
        if (_showing || value < 0 || value >= _templates.Count)
        {
            return;
        }

        _showing = true;
        TemplateText = _templates[value].Text;
        TemplateName = _templates[value].Name;
        IsRegex = _templates[value].IsRegex;
        _showing = false;
    }

    partial void OnTemplateNameChanged(string value)
    {
        ChangeSelected(template => template with { Name = value });
        ShowTemplateNames();
    }

    partial void OnTemplateTextChanged(string value) => ChangeSelected(template => template with { Text = value });

    partial void OnIsRegexChanged(bool value) => ChangeSelected(template => template with { IsRegex = value });

    [RelayCommand]
    private void Ok()
    {
        _preferences.CommitValidation = new CommitValidationOptions(
            (int)(MaxFirstLineLength ?? 0),
            (int)(MaxLineLength ?? 0),
            SecondLineMustBeEmpty,
            IndentAfterFirstLine,
            AutoWrap,
            ValidationRegex ?? "");
        _preferences.CommitTemplates = CommitTemplates.Serialize(_templates);
        Host.Close(true);
    }

    [RelayCommand]
    private void Cancel() => Host.Close(false);

    private void ChangeSelected(Func<CommitTemplate, CommitTemplate> change)
    {
        if (!_showing && SelectedIndex >= 0 && SelectedIndex < _templates.Count)
        {
            _templates[SelectedIndex] = change(_templates[SelectedIndex]);
        }
    }

    // The list's lines follow the names; the selection is kept.
    private void ShowTemplateNames()
    {
        IReadOnlyList<string> names = Names();
        if (TemplateNames.SequenceEqual(names))
        {
            return;
        }

        int selected = SelectedIndex;
        _showing = true;
        TemplateNames = names;
        SelectedIndex = selected;
        _showing = false;
    }

    private IReadOnlyList<string> Names()
        =>
        [
            .. _templates.Select((template, index) =>
                $"{index + 1} : {(template.Name.Length > 0 ? template.Name.ShortenTo(MaxShownCharsForName) : $"<{_emptyTemplate}>")}")
        ];
}
