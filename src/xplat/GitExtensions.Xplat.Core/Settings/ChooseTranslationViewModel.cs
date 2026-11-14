using CommunityToolkit.Mvvm.Input;
using GitExtensions.Xplat.Core.Presentation;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A language of upstream's translations, with its flag's file when the Translation folder has one.
/// </summary>
public sealed record TranslationChoice(string Name, string? FlagPath);

/// <summary>
///  The view model of upstream's <c>FormChooseTranslation</c>: the languages (English first, then upstream's translations
///  in order) with their flags. Choosing one sets upstream's <c>translation</c> setting and closes the window; closed
///  without a choice (<see cref="OnClosing"/>), the language is English, so the question is not asked again.
/// </summary>
public sealed partial class ChooseTranslationViewModel : DialogViewModel
{
    /// <summary>
    ///  Upstream's default language, which has no translation file.
    /// </summary>
    public const string English = "English";

    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "FormChooseTranslation";

    private readonly IAppPreferences _preferences;

    /// <param name="preferences">Where upstream's <c>translation</c> setting is kept.</param>
    /// <param name="translations">Upstream's translations (<c>Translator.GetAllTranslations</c>).</param>
    /// <param name="translationDirectory">Upstream's Translation folder, where the flags are.</param>
    public ChooseTranslationViewModel(IDialogHost host, IAppPreferences preferences, IEnumerable<string> translations,
        string translationDirectory)
        : base(host)
    {
        _preferences = preferences;
        Choices = [.. Languages(translations).Select(name => new TranslationChoice(name, FlagPath(translationDirectory, name)))];
    }

    public IReadOnlyList<TranslationChoice> Choices { get; }

    /// <summary>
    ///  Upstream's <c>OnLoad</c> list: English, then the translations sorted.
    /// </summary>
    public static IReadOnlyList<string> Languages(IEnumerable<string> translations)
    {
        List<string> sorted = [.. translations];
        sorted.Sort(StringComparer.CurrentCulture);
        sorted.Remove(English);
        return [English, .. sorted];
    }

    /// <summary>
    ///  Upstream's <c>FormChooseTranslation_FormClosing</c>: without a choice, the language is English.
    /// </summary>
    public void OnClosing()
    {
        if (string.IsNullOrEmpty(_preferences.Translation))
        {
            _preferences.Translation = English;
            _preferences.Save();
        }
    }

    // Upstream's lvTranslations_ItemActivate: the language is the user's choice and the window closes.
    [RelayCommand]
    private void Choose(TranslationChoice choice)
    {
        _preferences.Translation = choice.Name;
        _preferences.Save();
        Host.Close(null);
    }

    private static string? FlagPath(string directory, string language)
    {
        string path = Path.Join(directory, language + ".gif");
        return File.Exists(path) ? path : null;
    }
}
