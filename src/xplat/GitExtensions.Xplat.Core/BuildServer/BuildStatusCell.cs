using GitExtensions.Extensibility.BuildServerIntegration;

namespace GitExtensions.Xplat.Core.BuildServer;

/// <summary>
///  A commit's build status as the grid's "Build Status" column shows it (upstream's <c>BuildStatusColumnProvider</c>): the
///  status symbol, the description and the tooltip of the latest build of the commit.
/// </summary>
public sealed class BuildStatusCell : ObservableObject
{
    private BuildInfo? _info;

    public BuildInfo? Info => _info;

    public BuildStatus Status => _info?.Status ?? BuildStatus.Unknown;

    /// <summary>
    ///  Upstream's status symbol (✔, ❌, ...), or empty without a build.
    /// </summary>
    public string Symbol => _info?.StatusSymbol ?? "";

    public string Text => _info?.Description ?? "";

    /// <summary>
    ///  Upstream's tooltip: the build's own, or its description.
    /// </summary>
    public string? Tooltip => _info is null ? null : _info.Tooltip ?? _info.Description;

    public string? Url => string.IsNullOrWhiteSpace(_info?.Url) ? null : _info.Url;

    public string? PullRequestUrl => string.IsNullOrWhiteSpace(_info?.PullRequestUrl) ? null : _info.PullRequestUrl;

    /// <summary>
    ///  As upstream's <c>OnBuildInfoUpdate</c>: a build replaces the shown one unless it started earlier. True when it did.
    /// </summary>
    public bool Apply(BuildInfo info)
    {
        if (_info is not null && info.StartDate < _info.StartDate)
        {
            return false;
        }

        Show(info);
        return true;
    }

    /// <summary>
    ///  Shows no build.
    /// </summary>
    public void Clear()
    {
        if (_info is not null)
        {
            Show(null);
        }
    }

    private void Show(BuildInfo? info)
    {
        _info = info;
        RaisePropertyChanged(nameof(Info));
        RaisePropertyChanged(nameof(Status));
        RaisePropertyChanged(nameof(Symbol));
        RaisePropertyChanged(nameof(Text));
        RaisePropertyChanged(nameof(Tooltip));
        RaisePropertyChanged(nameof(Url));
        RaisePropertyChanged(nameof(PullRequestUrl));
    }
}
