using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace GitExtensions.Xplat.App;

// Upstream's ChecklistSettingsPage, whose state is the view model's ChecklistPageViewModel: here the translation window its
// language row opens, and the theme's editor background its colors are dimmed toward.
public partial class SettingsWindow
{
    /// <summary>
    ///  The rows the Checklist page shows, once its checks ran.
    /// </summary>
    public IReadOnlyList<Core.Settings.ChecklistEntry> ChecklistEntries => ViewModel.Checklist.Entries;

    /// <summary>
    ///  True when every checked setting is right.
    /// </summary>
    public bool IsChecklistValid => ViewModel.Checklist.IsValid;

    // Upstream's translationConfig_Click: the language chosen in FormChooseTranslation.
    private Task ChooseLanguageAsync() => new ChooseTranslationWindow(_preferences).ShowDialog(this);

    // Upstream dims the rows' colors toward the editor background in a dark theme.
    private System.Drawing.Color? DarkEditorBackground()
    {
        if (ActualThemeVariant != ThemeVariant.Dark)
        {
            return null;
        }

        return this.TryFindResource(ThemeApplier.EditorBackgroundKey, ActualThemeVariant, out object? resource) &&
               resource is ISolidColorBrush { Color: var color }
            ? System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)
            : System.Drawing.Color.Black;
    }
}
