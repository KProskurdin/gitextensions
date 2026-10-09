using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.DeleteUnusedBranches;

/// <summary>
///  Stands in for upstream's WinForms <c>DeleteUnusedBranchesForm</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class DeleteUnusedBranchesForm(DeleteUnusedBranchesFormSettings settings, IGitModule gitCommands,
    IGitUICommands? gitUiCommands, IGitPlugin gitPlugin) : PluginDialog
{
    private DeleteUnusedBranchesWindow? _window;

    /// <summary>
    ///  True when a branch was deleted, so the plugin asks the browse window to refresh.
    /// </summary>
    public bool HasDeletedBranch => _window?.HasDeletedBranch is true;

    protected override Window CreateWindow()
        => _window = new DeleteUnusedBranchesWindow(settings, gitCommands,
            gitUiCommands ?? throw new InvalidOperationException("No repository is open."), gitPlugin);
}
