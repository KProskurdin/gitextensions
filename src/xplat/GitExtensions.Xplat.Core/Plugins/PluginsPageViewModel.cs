using CommunityToolkit.Mvvm.ComponentModel;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  A plugin in the settings tree, by name.
/// </summary>
public sealed record PluginOption(IGitPlugin Plugin)
{
    public override string ToString() => Plugin.Name ?? "";
}

/// <summary>
///  A plugin file that could not be loaded, listed after the plugins.
/// </summary>
public sealed record SkippedPluginOption(SkippedPlugin File)
{
    public override string ToString() => $"{File.FileName} (not loaded)";
}

/// <summary>
///  The view model of upstream's plugin settings pages: the settings of the plugin chosen in the tree at the settings
///  source the page's header shows, or why a plugin file was not loaded. The values edited at each source are stored on
///  OK.
/// </summary>
public sealed partial class PluginsPageViewModel : ObservableObject
{
    // Upstream's texts.
    public const string NoSettings = "There are no settings available for this plugin.";
    public const string NotLoaded = "This plugin is not loaded. {0}";

    private readonly Func<SettingLevel, SettingsSource> _open;
    private readonly Dictionary<IGitPlugin, PluginSettingsEditor> _editors = [];
    private readonly Dictionary<SettingLevel, SettingsSource> _sources = [];
    private SettingLevel _level = SettingLevel.Effective;

    /// <param name="plugins">The loaded plugins.</param>
    /// <param name="skipped">The plugin files the loader passed over, with the reason.</param>
    /// <param name="open">Opens the plugins' settings at a source.</param>
    public PluginsPageViewModel(IReadOnlyList<IGitPlugin> plugins, IReadOnlyList<SkippedPlugin> skipped,
        Func<SettingLevel, SettingsSource> open)
    {
        _open = open;

        // As upstream's settings tree: the plugins that have settings, by title; then the files upstream's loader passed
        // over (WinForms plugins, other interface versions), with the reason.
        Pages =
        [
            .. plugins.Where(plugin => plugin.HasSettings).Select(plugin => new PluginOption(plugin))
                .OrderBy(option => option.ToString(), StringComparer.CurrentCultureIgnoreCase),
            .. skipped.Select(file => new SkippedPluginOption(file)),
        ];
    }

    /// <summary>
    ///  The pages of the settings tree's plugins node.
    /// </summary>
    public IReadOnlyList<object> Pages { get; }

    /// <summary>
    ///  The plugin shown: a <see cref="PluginOption"/>, a <see cref="SkippedPluginOption"/> or null.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSkipped), nameof(SkippedText), nameof(SkippedPath))]
    public partial object? Selected { get; private set; }

    /// <summary>
    ///  The settings of the plugin shown, or null.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Rows), nameof(HasNoSettings))]
    public partial PluginSettingsEditor? Editor { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    public IReadOnlyList<PluginSettingRow> Rows => Editor?.Rows ?? [];

    public bool HasNoSettings => Editor is { Rows.Count: 0 };

    /// <summary>
    ///  A skipped plugin file is shown; it has no settings source to choose.
    /// </summary>
    public bool IsSkipped => Selected is SkippedPluginOption;

    public string SkippedText => Selected is SkippedPluginOption { File: var file } ? string.Format(NotLoaded, file.Reason) : "";

    public string SkippedPath => Selected is SkippedPluginOption { File: var file } ? file.Path : "";

    /// <summary>
    ///  Shows a page of the tree's plugins node.
    /// </summary>
    public void Show(object? page)
    {
        Selected = page;
        ShowSelected();
    }

    /// <summary>
    ///  Sets the settings source a page shows first, from the page's header.
    /// </summary>
    public void SetLevel(SettingLevel level) => _level = level;

    /// <summary>
    ///  As upstream's settings page header: the values edited so far go to the source they were edited in (written on
    ///  OK), and the page shows the newly chosen source.
    /// </summary>
    public void ChangeLevel(SettingLevel level)
    {
        foreach (PluginSettingsEditor editor in _editors.Values)
        {
            editor.Save();
        }

        _editors.Clear();
        _level = level;
        ShowSelected();
    }

    /// <summary>
    ///  Stores the values edited at each source in their sources, which <paramref name="store"/> writes.
    /// </summary>
    public void Save(Action<SettingsSource> store)
    {
        foreach (PluginSettingsEditor editor in _editors.Values)
        {
            editor.Save();
        }

        foreach (SettingsSource source in _sources.Values)
        {
            store(source);
        }
    }

    private void ShowSelected()
    {
        if (Selected is SkippedPluginOption { File: var file })
        {
            Title = file.FileName;
            Editor = null;
            return;
        }

        Editor = Selected is PluginOption { Plugin: var plugin } ? EditorOf(plugin) : null;
        Title = Editor?.Title ?? "";
    }

    /// <summary>
    ///  A plugin's settings: one editor per plugin, at the settings source chosen when it was first shown (the settings
    ///  search reads them too).
    /// </summary>
    public PluginSettingsEditor EditorOf(IGitPlugin plugin)
    {
        if (!_editors.TryGetValue(plugin, out PluginSettingsEditor? editor))
        {
            editor = new PluginSettingsEditor(plugin, SourceAt(_level));
            _editors[plugin] = editor;
        }

        return editor;
    }

    private SettingsSource SourceAt(SettingLevel level)
    {
        if (!_sources.TryGetValue(level, out SettingsSource? source))
        {
            _sources[level] = source = _open(level);
        }

        return source;
    }
}
