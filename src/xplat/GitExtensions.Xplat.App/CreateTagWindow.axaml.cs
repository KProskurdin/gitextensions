using Avalonia.Controls;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>FormCreateTag</c>, bound to <see cref="CreateTagViewModel"/> (rules in <see cref="CreateTagDialog"/>): a
///  lightweight, annotated or signed tag on a commit, pushed to the current remote when asked. Closes with true when the tag
///  was created.
/// </summary>
public partial class CreateTagWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CreateTagWindow() => InitializeComponent();
#pragma warning restore CS8618

    /// <param name="repositoryPath">The repository the tag is created in.</param>
    /// <param name="revision">The commit to tag, or null (or an artificial commit) for the current checkout.</param>
    /// <param name="runGitWithOutput">Runs the tag command with its output shown (upstream's process dialog).</param>
    /// <param name="pushTag">Pushes a tag to a remote (remote, tag), with upstream's push scripts around it.</param>
    public CreateTagWindow(string repositoryPath, string? revision, Func<string, Task<bool>> runGitWithOutput,
        Func<string, string, Task<bool>> pushTag)
    {
        ViewModel = new CreateTagViewModel(new WindowDialogHost(this), GitModules.Open(repositoryPath), revision,
            runGitWithOutput, pushTag);
        DataContext = ViewModel;
        InitializeComponent();
        UpstreamTranslation.Apply(this, CreateTagViewModel.Category);
        InitialFocus.Attach(this, () => textBoxTagName);
        Opened += (_, _) => UiActions.Run(ViewModel.LoadAsync, ex => _ = new ErrorWindow(ex.Message).ShowDialog(this));
    }

    public CreateTagViewModel ViewModel { get; }

    /// <summary>
    ///  Completes when the commit and the remote are shown, for tests.
    /// </summary>
    public Task TagLoaded => ViewModel.Loaded;
}
