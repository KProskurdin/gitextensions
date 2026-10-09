using Avalonia.Controls;
using GitCommands;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's version of upstream's <c>FormCommitTemplateSettings</c>, opened from the commit window's templates menu:
///  the user's ten commit templates and the commit validation settings, stored under upstream's keys when OK is pressed.
/// </summary>
public partial class CommitTemplateSettingsWindow : Window
{
    private const string Category = "FormCommitTemplateSettings";
    private const int MaxShownCharsForName = 50;

    private readonly IAppPreferences _preferences;
    private readonly List<CommitTemplate> _templates;
    private bool _showing;

    public CommitTemplateSettingsWindow(IAppPreferences preferences)
    {
        InitializeComponent();
        UpstreamTranslation.Apply(this, Category);
        _preferences = preferences;
        _templates = [.. CommitTemplates.Slots(preferences.CommitTemplates)];

        CommitValidationOptions validation = preferences.CommitValidation;
        _NO_TRANSLATE_numericMaxFirstLineLength.Value = validation.MaxFirstLineLength;
        _NO_TRANSLATE_numericMaxLineLength.Value = validation.MaxLineLength;
        checkBoxSecondLineEmpty.IsChecked = validation.SecondLineMustBeEmpty;
        checkBoxUseIndent.IsChecked = validation.IndentAfterFirstLine;
        checkBoxAutoWrap.IsChecked = validation.AutoWrap;
        _NO_TRANSLATE_textBoxCommitValidationRegex.Text = validation.RegEx;

        ShowTemplateNames();
        _NO_TRANSLATE_comboBoxCommitTemplates.SelectionChanged += (_, _) => ShowSelectedTemplate();
        _NO_TRANSLATE_comboBoxCommitTemplates.SelectedIndex = 0;
        _NO_TRANSLATE_textBoxCommitTemplateName.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ChangeSelected(template => template with { Name = _NO_TRANSLATE_textBoxCommitTemplateName.Text ?? "" });
                ShowTemplateNames();
            }
        };
        _NO_TRANSLATE_textCommitTemplateText.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ChangeSelected(template => template with { Text = _NO_TRANSLATE_textCommitTemplateText.Text ?? "" });
            }
        };
        checkBoxRegexEnabled.IsCheckedChanged += (_, _) =>
            ChangeSelected(template => template with { IsRegex = checkBoxRegexEnabled.IsChecked == true });
        buttonOk.Click += (_, _) =>
        {
            Save();
            Close(true);
        };
        buttonCancel.Click += (_, _) => Close(false);
    }

    private static string EmptyTemplate => UpstreamTranslation.Text(Category, "_emptyTemplate", "empty");

    private void Save()
    {
        _preferences.CommitValidation = new CommitValidationOptions(
            (int)(_NO_TRANSLATE_numericMaxFirstLineLength.Value ?? 0),
            (int)(_NO_TRANSLATE_numericMaxLineLength.Value ?? 0),
            checkBoxSecondLineEmpty.IsChecked == true,
            checkBoxUseIndent.IsChecked == true,
            checkBoxAutoWrap.IsChecked == true,
            _NO_TRANSLATE_textBoxCommitValidationRegex.Text ?? "");
        _preferences.CommitTemplates = CommitTemplates.Serialize(_templates);
    }

    private void ChangeSelected(Func<CommitTemplate, CommitTemplate> change)
    {
        int index = _NO_TRANSLATE_comboBoxCommitTemplates.SelectedIndex;
        if (!_showing && index >= 0)
        {
            _templates[index] = change(_templates[index]);
        }
    }

    private void ShowSelectedTemplate()
    {
        int index = _NO_TRANSLATE_comboBoxCommitTemplates.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        _showing = true;
        _NO_TRANSLATE_textCommitTemplateText.Text = _templates[index].Text;
        _NO_TRANSLATE_textBoxCommitTemplateName.Text = _templates[index].Name;
        checkBoxRegexEnabled.IsChecked = _templates[index].IsRegex;
        _showing = false;
    }

    // Upstream's RefreshLineInListBox: "1 : name", or "1 : <empty>" for a template without a name.
    private void ShowTemplateNames()
    {
        int selected = _NO_TRANSLATE_comboBoxCommitTemplates.SelectedIndex;
        List<string> names = [.. _templates.Select((template, index) =>
            $"{index + 1} : {(template.Name.Length > 0 ? template.Name.ShortenTo(MaxShownCharsForName) : $"<{EmptyTemplate}>")}")];
        if (_NO_TRANSLATE_comboBoxCommitTemplates.ItemsSource is List<string> shown && shown.SequenceEqual(names))
        {
            return;
        }

        _showing = true;
        _NO_TRANSLATE_comboBoxCommitTemplates.ItemsSource = names;
        _NO_TRANSLATE_comboBoxCommitTemplates.SelectedIndex = selected;
        _showing = false;
    }
}
