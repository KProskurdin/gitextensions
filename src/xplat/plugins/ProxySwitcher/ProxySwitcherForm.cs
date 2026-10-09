using Avalonia.Controls;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Xplat.Ui;

namespace GitExtensions.Plugins.ProxySwitcher;

/// <summary>
///  Stands in for upstream's WinForms <c>ProxySwitcherForm</c>, which the unchanged plugin class creates and shows.
/// </summary>
public sealed class ProxySwitcherForm(ProxySwitcherPlugin plugin, SettingsSource settings, GitUIEventArgs gitUiCommands)
    : PluginDialog
{
    protected override Window CreateWindow() => new ProxySwitcherWindow(plugin, settings, gitUiCommands.GitModule);
}
