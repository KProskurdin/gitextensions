using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  A language of upstream's translations, with its flag when the Translation folder has one.
/// </summary>
public sealed record TranslationChoice(string Name, Bitmap? Flag);

/// <summary>
///  Upstream's <c>FormChooseTranslation</c>: the languages (English first, then upstream's translations in order) with
///  their flags; the item under the mouse is selected and a click chooses it (upstream's <c>HoverSelection</c> and
///  one-click activation), which sets upstream's <c>translation</c> setting and closes the window. Closed without a choice,
///  the language is English, so the question is not asked again.
/// </summary>
public partial class ChooseTranslationWindow : Window
{
    // Upstream's default language, which has no translation file.
    public const string English = "English";

    private const string FormName = "FormChooseTranslation";

    private readonly IAppPreferences _preferences;

    public ChooseTranslationWindow()
        : this(AppServices.Preferences)
    {
    }

    public ChooseTranslationWindow(IAppPreferences preferences)
    {
        _preferences = preferences;
        InitializeComponent();
        UpstreamTranslation.Apply(this, FormName);
        lvTranslations.ItemsSource = Languages().Select(name => new TranslationChoice(name, FlagOf(name))).ToList();
        lvTranslations.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is
                TranslationChoice choice)
            {
                lvTranslations.SelectedItem = choice;
            }
        }, handledEventsToo: true);
        lvTranslations.Tapped += (_, e) =>
        {
            if ((e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is
                TranslationChoice choice)
            {
                Choose(choice);
            }
        };
        lvTranslations.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && lvTranslations.SelectedItem is TranslationChoice choice)
            {
                e.Handled = true;
                Choose(choice);
            }
        };
        Closing += (_, _) =>
        {
            // Upstream's FormChooseTranslation_FormClosing.
            if (string.IsNullOrEmpty(_preferences.Translation))
            {
                _preferences.Translation = English;
                _preferences.Save();
            }
        };
    }

    /// <summary>
    ///  Upstream's <c>OnLoad</c> list: English, then the translations sorted.
    /// </summary>
    public static IReadOnlyList<string> Languages()
    {
        List<string> translations = [.. Translator.GetAllTranslations()];
        translations.Sort(StringComparer.CurrentCulture);
        translations.Remove(English);
        return [English, .. translations];
    }

    /// <summary>
    ///  Upstream's <c>lvTranslations_ItemActivate</c>: the language is the user's choice and the window closes.
    /// </summary>
    public void Choose(TranslationChoice choice)
    {
        _preferences.Translation = choice.Name;
        _preferences.Save();
        Close();
    }

    private static Bitmap? FlagOf(string language)
    {
        string path = Path.Join(Translator.GetTranslationDir(), language + ".gif");
        try
        {
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            // As upstream, a language whose image cannot be read is listed without it.
            return null;
        }
    }
}
