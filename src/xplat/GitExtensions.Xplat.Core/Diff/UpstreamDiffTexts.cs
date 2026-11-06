using GitCommands;
using GitExtensions.Extensibility.Translations;
using ResourceManager;

namespace GitUI;

/// <summary>
///  Stands in for upstream's GitUI <c>TranslatedStrings</c> (a WinForms project) with the texts of the upstream
///  <c>FileStatusDiffCalculator</c> the core links and of the file list menu's "Diff with" item, under upstream's field
///  names, so upstream's translations fill them as they fill upstream's class.
/// </summary>
internal sealed class TranslatedStrings : Translate
{
    private static Lazy<TranslatedStrings> _instance = new();

    private readonly TranslationString _diffWithParent = new("Diff with A ");
    private readonly TranslationString _diffBaseWith = new("Diff BASE with");
    private readonly TranslationString _diffRange = new("Range diff");
    private readonly TranslationString _combinedDiff = new("Combined diff");
    private readonly TranslationString _diffSelectedWithRememberedFile = new("&Diff with \"{0}\"");

    public TranslatedStrings()
    {
        Translator.Translate(this, AppSettings.CurrentTranslation);
    }

    public static string DiffWithParent => _instance.Value._diffWithParent.Text;

    public static string DiffBaseWith => _instance.Value._diffBaseWith.Text;

    public static string DiffRange => _instance.Value._diffRange.Text;

    public static string CombinedDiff => _instance.Value._combinedDiff.Text;

    public static string DiffSelectedWithRememberedFile => _instance.Value._diffSelectedWithRememberedFile.Text;

    /// <summary>
    ///  Reads the texts again after the language changed, as upstream's <c>Reinitialize</c>.
    /// </summary>
    public static void Reinitialize()
    {
        if (_instance.IsValueCreated)
        {
            _instance = new();
        }
    }
}
