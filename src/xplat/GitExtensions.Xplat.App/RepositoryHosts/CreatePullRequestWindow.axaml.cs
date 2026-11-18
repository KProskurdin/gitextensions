using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Core.RepositoryHosts;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Xplat.App.RepositoryHosts;

/// <summary>
///  The new shell's version of upstream's <c>CreatePullRequestForm</c>, bound to <see cref="CreatePullRequestViewModel"/>.
/// </summary>
public partial class CreatePullRequestWindow : Window
{
    /// <summary>
    ///  The design-time constructor (<see cref="DesignTime"/>): the window's XAML only.
    /// </summary>
    [Obsolete(DesignTime.ConstructorOnly)]
#pragma warning disable CS8618 // The fields the window's data fills stay unset: no code of the window runs.
    public CreatePullRequestWindow() => InitializeComponent();
#pragma warning restore CS8618

    public CreatePullRequestWindow(IRepositoryHostPlugin repoHost, IGitModule module, string? chooseRemote = null)
    {
        ViewModel = new CreatePullRequestViewModel(new WindowDialogHost(this), RepositoryHostMessages.For(this), repoHost,
            module, chooseRemote);
        InitializeComponent();
        UpstreamTranslation.Apply(this, CreatePullRequestViewModel.Category);
        DataContext = ViewModel;

        // View concern: the hosted repositories show their display data.
        _pullReqTargetsCB.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(IHostedRemote.DisplayData));
        Opened += (_, _) => _ = ViewModel.LoadAsync();
    }

    public CreatePullRequestViewModel ViewModel { get; }

    /// <summary>
    ///  True once the target and the user's branches are listed.
    /// </summary>
    public bool AreBranchesLoaded => ViewModel.AreBranchesLoaded;
}
