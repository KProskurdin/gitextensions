using Avalonia.Controls;
using GitExtensions.Xplat.Core.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The new shell's version of upstream's <c>FormCommitTemplateSettings</c>, bound to
///  <see cref="CommitTemplateSettingsViewModel"/> and opened from the commit window's templates menu: the user's ten commit
///  templates and the commit validation settings, stored under upstream's keys when OK is pressed.
/// </summary>
public partial class CommitTemplateSettingsWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
    public CommitTemplateSettingsWindow() => InitializeComponent();

    public CommitTemplateSettingsWindow(IAppPreferences preferences)
    {
        DataContext = new CommitTemplateSettingsViewModel(new WindowDialogHost(this), preferences);
        InitializeComponent();
        UpstreamTranslation.Apply(this, CommitTemplateSettingsViewModel.Category);
    }
}
