using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.ReleaseNotesGenerator;

/// <summary>
///  Stands in for upstream's WinForms <c>ReleaseNotesGeneratorForm</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class ReleaseNotesGeneratorForm(GitUIEventArgs gitUiCommands) : PluginDialog
{
    protected override Window CreateWindow() => new ReleaseNotesGeneratorWindow(gitUiCommands.GitModule);
}
