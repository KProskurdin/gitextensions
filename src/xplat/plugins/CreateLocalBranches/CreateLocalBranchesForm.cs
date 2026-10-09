using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.CreateLocalBranches;

/// <summary>
///  Stands in for upstream's WinForms <c>CreateLocalBranchesForm</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class CreateLocalBranchesForm(GitUIEventArgs gitUiCommands) : PluginDialog
{
    protected override Window CreateWindow() => new CreateLocalBranchesWindow(gitUiCommands.GitModule);
}
