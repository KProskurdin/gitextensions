using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Core.RepositoryHosts;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The new shell's version of upstream's <c>ForkAndCloneForm</c>, bound to <see cref="ForkAndCloneViewModel"/>; the folder
///  picker is the view model's delegate.
/// </summary>
public partial class ForkAndCloneWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public ForkAndCloneWindow() => InitializeComponent();
#pragma warning restore CS8618

    public ForkAndCloneWindow(IRepositoryHostPlugin gitHoster, RepositoryOperationsViewModel actions, string defaultDestination,
        EventHandler<GitModuleEventArgs>? gitModuleChanged = null)
    {
        ViewModel = new ForkAndCloneViewModel(new WindowDialogHost(this), RepositoryHostMessages.For(this), gitHoster, actions,
            defaultDestination, new ForkAndCloneServices
            {
                PickFolder = PickFolderAsync,
                Cloned = gitModuleChanged is null ? null : module => gitModuleChanged(this, new GitModuleEventArgs(module)),
            });
        InitializeComponent();
        UpstreamTranslation.Apply(this, ForkAndCloneViewModel.Category);
        Title = $"{gitHoster.Name}: {Title}";

        // The help text as translated, which the view model extends with an error; then the bindings take over.
        ViewModel.HelpText = helpTextLbl.Text;
        helpTextLbl.Bind(TextBlock.TextProperty, new Binding(nameof(ForkAndCloneViewModel.HelpText)));
        DataContext = ViewModel;
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public ForkAndCloneViewModel ViewModel { get; }

    private async Task<string?> PickFolderAsync(string? start)
    {
        IStorageFolder? folder = start is null ? null : await StorageProvider.TryGetFolderFromPathAsync(start);
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { SuggestedStartLocation = folder });
        return folders is [{ } picked] ? picked.TryGetLocalPath() : null;
    }
}
