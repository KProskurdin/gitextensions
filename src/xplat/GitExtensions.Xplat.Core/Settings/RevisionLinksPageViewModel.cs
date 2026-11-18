using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  The view model of upstream's Revision links settings page: the definitions stored at the chosen settings level, the
///  selected one (whose fields the page edits), and Add, the templates, Remove and the selected one's links. The window
///  keeps the definitions of every level it showed and stores them on OK.
/// </summary>
public sealed partial class RevisionLinksPageViewModel : ObservableObject
{
    private readonly Func<SettingLevel, RevisionLinkEditor> _editorAt;
    private readonly Func<Task<IReadOnlyList<Remote>>> _loadRemotes;

    /// <param name="editorAt">The definitions stored at a level, kept by the window for OK.</param>
    /// <param name="loadRemotes">The repository's remotes, which the templates read.</param>
    public RevisionLinksPageViewModel(Func<SettingLevel, RevisionLinkEditor> editorAt,
        Func<Task<IReadOnlyList<Remote>>> loadRemotes)
    {
        _editorAt = editorAt;
        _loadRemotes = loadRemotes;
        Editor = editorAt(SettingLevel.Effective);
        Selected = Editor.Items.FirstOrDefault();
    }

    /// <summary>
    ///  The definitions of the level shown.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Items))]
    public partial RevisionLinkEditor Editor { get; private set; }

    public ObservableCollection<RevisionLinkItem> Items => Editor.Items;

    /// <summary>
    ///  Upstream's templates (GitHub, Azure DevOps), each a button of its own here.
    /// </summary>
    public IReadOnlyList<ICloudProviderExternalLinkDefinitionExtractor> Templates => RevisionLinkTemplates.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand), nameof(AddFormatCommand))]
    public partial RevisionLinkItem? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    /// <summary>
    ///  As upstream's settings page header: the page shows the definitions stored at the chosen level; what was edited at
    ///  the other levels is stored too on OK.
    /// </summary>
    public void ChangeLevel(SettingLevel level)
    {
        Editor = _editorAt(level);
        Selected = Editor.Items.FirstOrDefault();
    }

    /// <summary>
    ///  Removes one of the selected definition's links.
    /// </summary>
    [RelayCommand]
    public void RemoveFormat(RevisionLinkFormatItem format) => Selected?.Formats.Remove(format);

    [RelayCommand]
    private void Add() => Selected = Editor.Add();

    // Upstream's template menu: the definitions for the repository's remotes; the first one added is selected.
    [RelayCommand]
    private async Task AddTemplateAsync(ICloudProviderExternalLinkDefinitionExtractor template)
    {
        IReadOnlyList<RevisionLinkItem> added = Editor.AddTemplates(template, await _loadRemotes());
        if (added.Count > 0)
        {
            Selected = added[0];
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (Selected is not { } item)
        {
            return;
        }

        int index = Items.IndexOf(item);
        Editor.Remove(item);
        Selected = Items.Count > 0 ? Items[Math.Min(index, Items.Count - 1)] : null;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void AddFormat() => Selected?.AddFormat();
}
