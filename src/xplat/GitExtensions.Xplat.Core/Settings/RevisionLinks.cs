using System.Collections.ObjectModel;
using GitCommands;
using GitCommands.ExternalLinks;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  Where upstream's revision link definitions (<c>RevisionLinkDefs</c>) are read and written.
/// </summary>
public interface IRevisionLinkStore
{
    /// <summary>
    ///  The repository's effective settings when one is open, as upstream's settings dialog edits them (the repository's
    ///  .git and shared GitExtensions.settings, then the user's), otherwise the user's.
    /// </summary>
    DistributedSettings Open(string? repositoryPath);

    /// <summary>
    ///  Writes the settings <see cref="Open"/> returned.
    /// </summary>
    void Save(DistributedSettings settings);
}

/// <summary>
///  The GitExtensions.settings files upstream uses.
/// </summary>
public sealed class UpstreamRevisionLinkStore : IRevisionLinkStore
{
    public DistributedSettings Open(string? repositoryPath)
        => string.IsNullOrEmpty(repositoryPath)
            ? DistributedSettings.CreateGlobal()
            : DistributedSettings.CreateEffective(
                new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath));

    public void Save(DistributedSettings settings) => settings.Save();
}

/// <summary>
///  One settings file for every repository. Tests use it with a temporary file, so they never write the user's settings.
/// </summary>
public sealed class FileRevisionLinkStore(string settingsFile) : IRevisionLinkStore
{
    public DistributedSettings Open(string? repositoryPath)
        => new(null, new GitExtSettingsCache(settingsFile, autoSave: false), SettingLevel.Global);

    public void Save(DistributedSettings settings) => settings.Save();
}

/// <summary>
///  The Revision links page's list (upstream's <c>RevisionLinksSettingsPage</c>): upstream's
///  <see cref="ExternalLinksManager"/> keeps the definitions at their levels and stores them as upstream does.
/// </summary>
public sealed class RevisionLinkEditor
{
    private readonly ExternalLinksManager _manager;

    public RevisionLinkEditor(DistributedSettings settings)
    {
        _manager = new ExternalLinksManager(settings);
        Items = [.. _manager.GetEffectiveSettings().Select(definition => new RevisionLinkItem(definition))];
    }

    public ObservableCollection<RevisionLinkItem> Items { get; }

    /// <summary>
    ///  Upstream's new definition: named "&lt;new&gt;", enabled, searching the message, with the remote data upstream sets.
    /// </summary>
    public RevisionLinkItem Add()
    {
        ExternalLinkDefinition definition = new()
        {
            Name = "<new>",
            Enabled = true,
            UseRemotesPattern = "upstream|origin",
            UseOnlyFirstRemote = true,
            SearchInParts = { ExternalLinkDefinition.RevisionPart.Message },
            RemoteSearchInParts = { ExternalLinkDefinition.RemotePart.URL },
        };
        return AddRange([definition])[0];
    }

    /// <summary>
    ///  Upstream's "Add {service} templates": the template's definitions for the preferred remote whose URL the service
    ///  recognises, or with placeholder names when there is none. A name already listed is not added again, as upstream.
    /// </summary>
    public IReadOnlyList<RevisionLinkItem> AddTemplates(ICloudProviderExternalLinkDefinitionExtractor template,
        IReadOnlyList<Remote> remotes)
    {
        Remote remote = RevisionLinkTemplates.PreferredRemote(
            [.. remotes.Where(candidate => template.IsValidRemoteUrl(candidate.FetchUrl))]);
        return AddRange([.. template.GetDefinitions(remote.FetchUrl)]);
    }

    public void Remove(RevisionLinkItem item)
    {
        _manager.Remove(item.Definition);
        Items.Remove(item);
    }

    /// <summary>
    ///  Stores the definitions as upstream's page does; upstream drops link rows without a caption or URI on save.
    /// </summary>
    public void Save()
    {
        foreach (RevisionLinkItem item in Items)
        {
            item.ApplyFormats();
        }

        _manager.Save();
    }

    private IReadOnlyList<RevisionLinkItem> AddRange(IReadOnlyList<ExternalLinkDefinition> definitions)
    {
        _manager.AddRange(definitions);
        IReadOnlyList<ExternalLinkDefinition> effective = _manager.GetEffectiveSettings();
        List<RevisionLinkItem> added = [];
        foreach (ExternalLinkDefinition definition in definitions.Where(effective.Contains))
        {
            RevisionLinkItem item = new(definition);
            Items.Add(item);
            added.Add(item);
        }

        return added;
    }
}

/// <summary>
///  Upstream's revision link templates.
/// </summary>
public static class RevisionLinkTemplates
{
    /// <summary>
    ///  The services upstream offers templates for, in upstream's order.
    /// </summary>
    public static IReadOnlyList<ICloudProviderExternalLinkDefinitionExtractor> All { get; } =
        [.. new CloudProviderExternalLinkDefinitionExtractorFactory().GetAllExtractor()];

    /// <summary>
    ///  Upstream's menu text for a service.
    /// </summary>
    public static string MenuText(ICloudProviderExternalLinkDefinitionExtractor template)
        => $"Add {template.ServiceName} templates";

    /// <summary>
    ///  Upstream's <c>FindRemoteByPreference</c>: "upstream", then "fork", then "origin", then the first remote; none when
    ///  there are no remotes.
    /// </summary>
    public static Remote PreferredRemote(IReadOnlyList<Remote> remotes)
    {
        if (remotes.Count == 0)
        {
            return default;
        }

        foreach (string name in (string[])["upstream", "fork", "origin"])
        {
            if (remotes.FirstOrDefault(remote => remote.Name == name) is { Name: not null } found)
            {
                return found;
            }
        }

        return remotes[0];
    }

    /// <summary>
    ///  The repository's remotes with their URLs, as upstream's page reads them for the templates.
    /// </summary>
    public static Task<IReadOnlyList<Remote>> LoadRemotesAsync(string? repositoryPath)
        => string.IsNullOrEmpty(repositoryPath)
            ? Task.FromResult<IReadOnlyList<Remote>>([])
            : Task.Run<IReadOnlyList<Remote>>(() => new GitModule(new GitExecutorProvider(new GitDirectoryResolver()), repositoryPath)
                .GetRemotesAsync());
}

/// <summary>
///  A revision link definition on the page. Each change goes to upstream's definition at once, as upstream's page writes
///  its fields when they are left.
/// </summary>
public sealed class RevisionLinkItem : ObservableObject
{
    public RevisionLinkItem(ExternalLinkDefinition definition)
    {
        Definition = definition;
        Formats = [.. definition.LinkFormats.Select(format => new RevisionLinkFormatItem(format.Caption, format.Format))];
    }

    public ExternalLinkDefinition Definition { get; }

    /// <summary>
    ///  The link rows: a caption and a URI each, with upstream's placeholders ({0}, %COMMIT_HASH%).
    /// </summary>
    public ObservableCollection<RevisionLinkFormatItem> Formats { get; }

    public string Name
    {
        get => Definition.Name ?? "";
        set => Change(() => Definition.Name = value);
    }

    public bool Enabled
    {
        get => Definition.Enabled;
        set => Change(() => Definition.Enabled = value);
    }

    public bool SearchMessage
    {
        get => Definition.SearchInParts.Contains(ExternalLinkDefinition.RevisionPart.Message);
        set => Change(() => Toggle(Definition.SearchInParts, ExternalLinkDefinition.RevisionPart.Message, value));
    }

    public bool SearchLocalBranches
    {
        get => Definition.SearchInParts.Contains(ExternalLinkDefinition.RevisionPart.LocalBranches);
        set => Change(() => Toggle(Definition.SearchInParts, ExternalLinkDefinition.RevisionPart.LocalBranches, value));
    }

    public bool SearchRemoteBranches
    {
        get => Definition.SearchInParts.Contains(ExternalLinkDefinition.RevisionPart.RemoteBranches);
        set => Change(() => Toggle(Definition.SearchInParts, ExternalLinkDefinition.RevisionPart.RemoteBranches, value));
    }

    // Upstream trims the patterns when their box is left.
    public string SearchPattern
    {
        get => Definition.SearchPattern ?? "";
        set => Change(() => Definition.SearchPattern = value.Trim());
    }

    public string NestedSearchPattern
    {
        get => Definition.NestedSearchPattern ?? "";
        set => Change(() => Definition.NestedSearchPattern = value.Trim());
    }

    public string UseRemotesPattern
    {
        get => Definition.UseRemotesPattern ?? "";
        set => Change(() => Definition.UseRemotesPattern = value.Trim());
    }

    public bool UseOnlyFirstRemote
    {
        get => Definition.UseOnlyFirstRemote;
        set => Change(() => Definition.UseOnlyFirstRemote = value);
    }

    public string RemoteSearchPattern
    {
        get => Definition.RemoteSearchPattern ?? "";
        set => Change(() => Definition.RemoteSearchPattern = value.Trim());
    }

    public bool SearchRemoteUrl
    {
        get => Definition.RemoteSearchInParts.Contains(ExternalLinkDefinition.RemotePart.URL);
        set => Change(() => Toggle(Definition.RemoteSearchInParts, ExternalLinkDefinition.RemotePart.URL, value));
    }

    public bool SearchRemotePushUrl
    {
        get => Definition.RemoteSearchInParts.Contains(ExternalLinkDefinition.RemotePart.PushURL);
        set => Change(() => Toggle(Definition.RemoteSearchInParts, ExternalLinkDefinition.RemotePart.PushURL, value));
    }

    public RevisionLinkFormatItem AddFormat()
    {
        RevisionLinkFormatItem format = new("", "");
        Formats.Add(format);
        return format;
    }

    public override string ToString() => Name;

    /// <summary>
    ///  Puts the link rows into upstream's definition.
    /// </summary>
    internal void ApplyFormats()
    {
        Definition.LinkFormats.Clear();
        foreach (RevisionLinkFormatItem format in Formats)
        {
            Definition.LinkFormats.Add(new ExternalLinkFormat { Caption = format.Caption, Format = format.Format });
        }
    }

    private static void Toggle<T>(HashSet<T> parts, T part, bool on)
    {
        if (on)
        {
            parts.Add(part);
        }
        else
        {
            parts.Remove(part);
        }
    }

    private void Change(Action change, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        change();
        RaisePropertyChanged(propertyName);
    }
}

/// <summary>
///  A link row of a revision link definition.
/// </summary>
public sealed class RevisionLinkFormatItem(string? caption, string? format) : ObservableObject
{
    private string _caption = caption ?? "";
    private string _format = format ?? "";

    public string Caption
    {
        get => _caption;
        set => SetProperty(ref _caption, value);
    }

    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }
}
