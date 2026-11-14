using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GitExtensions.Extensibility.Translations;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormChooseTranslation</c>, bound to <see cref="ChooseTranslationViewModel"/>: the languages with their
///  flags; the item under the mouse is selected and a click chooses it (upstream's <c>HoverSelection</c> and one-click
///  activation). Closed without a choice, the language is English, so the question is not asked again.
/// </summary>
public partial class ChooseTranslationWindow : Window
{
    /// <summary>
    ///  Upstream's default language, which has no translation file.
    /// </summary>
    public const string English = ChooseTranslationViewModel.English;

    public ChooseTranslationWindow()
        : this(AppServices.Preferences)
    {
    }

    public ChooseTranslationWindow(IAppPreferences preferences)
    {
        ViewModel = new ChooseTranslationViewModel(new WindowDialogHost(this), preferences,
            Translator.GetAllTranslations(), Translator.GetTranslationDir());
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, ChooseTranslationViewModel.Category);

        // View concerns: upstream's hover selection, and activation with one click or Enter.
        lvTranslations.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (ChoiceAt(e.Source) is { } choice)
            {
                lvTranslations.SelectedItem = choice;
            }
        }, handledEventsToo: true);
        lvTranslations.Tapped += (_, e) =>
        {
            if (ChoiceAt(e.Source) is { } choice)
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
        Closing += (_, _) => ViewModel.OnClosing();
    }

    public ChooseTranslationViewModel ViewModel { get; }

    /// <summary>
    ///  Upstream's <c>OnLoad</c> list: English, then the translations sorted.
    /// </summary>
    public static IReadOnlyList<string> Languages() =>
        ChooseTranslationViewModel.Languages(Translator.GetAllTranslations());

    /// <summary>
    ///  Upstream's <c>lvTranslations_ItemActivate</c>: the language is the user's choice and the window closes.
    /// </summary>
    public void Choose(TranslationChoice choice) => ViewModel.ChooseCommand.Execute(choice);

    private static TranslationChoice? ChoiceAt(object? source)
        => (source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as TranslationChoice;
}
