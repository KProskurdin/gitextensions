using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.GitImpact;

/// <summary>
///  Stands in for upstream's WinForms <c>FormImpact</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class FormImpact(IGitModule module) : PluginDialog
{
    protected override Window CreateWindow() => new ImpactWindow(module);
}
