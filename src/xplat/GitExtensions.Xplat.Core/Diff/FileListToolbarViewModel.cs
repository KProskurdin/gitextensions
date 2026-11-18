using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Diff;

/// <summary>
///  Upstream <c>FileStatusList</c>'s toolbar over a list of a commit's or a stash's files: the collapse button, the grouping
///  (<see cref="FileListGroupingViewModel"/>) and the diff status filter (<c>btnUnequalChange</c>, <c>btnOnlyB</c>,
///  <c>btnOnlyA</c>, <c>btnSameChange</c>), shown while the list has a BASE group. The view's list shows its files again on
///  <see cref="Changed"/> and collapses its groups on <see cref="CollapseGroupsRequested"/>; its tree stays the view's.
/// </summary>
public sealed partial class FileListToolbarViewModel : ObservableObject
{
    public FileListToolbarViewModel(IAppPreferences preferences)
    {
        Grouping = new FileListGroupingViewModel(preferences);
        Grouping.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///  Raised after the grouping or the diff status filter changed.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    ///  Upstream's <c>CollapseGroups_Click</c>: the list collapses its groups, or expands the selected one.
    /// </summary>
    public event EventHandler? CollapseGroupsRequested;

    public FileListGroupingViewModel Grouping { get; }

    /// <summary>
    ///  Upstream shows the collapse button for a list with groups to collapse.
    /// </summary>
    [ObservableProperty]
    public partial bool IsCollapseGroupsVisible { get; set; } = true;

    /// <summary>
    ///  Upstream's <c>UpdateToolbar</c>: the diff status filter while the list has a BASE group.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDiffStatusFilterVisible { get; set; }

    [ObservableProperty]
    public partial bool ShowsUnequalChange { get; private set; } = true;

    [ObservableProperty]
    public partial bool ShowsOnlyBChange { get; private set; } = true;

    [ObservableProperty]
    public partial bool ShowsOnlyAChange { get; private set; } = true;

    [ObservableProperty]
    public partial bool ShowsSameChange { get; private set; } = true;

    /// <summary>
    ///  Upstream's <c>IsDiffStatusMatch</c>: whether the filter shows a file of a BASE group with <paramref name="status"/>.
    /// </summary>
    public bool Shows(DiffBranchStatus status) => status switch
    {
        DiffBranchStatus.UnequalChange => ShowsUnequalChange,
        DiffBranchStatus.OnlyBChange => ShowsOnlyBChange,
        DiffBranchStatus.OnlyAChange => ShowsOnlyAChange,
        DiffBranchStatus.SameChange => ShowsSameChange,
        _ => true,
    };

    [RelayCommand]
    private void ToggleDiffStatus(DiffBranchStatus status)
    {
        switch (status)
        {
            case DiffBranchStatus.UnequalChange:
                ShowsUnequalChange = !ShowsUnequalChange;
                break;
            case DiffBranchStatus.OnlyBChange:
                ShowsOnlyBChange = !ShowsOnlyBChange;
                break;
            case DiffBranchStatus.OnlyAChange:
                ShowsOnlyAChange = !ShowsOnlyAChange;
                break;
            case DiffBranchStatus.SameChange:
                ShowsSameChange = !ShowsSameChange;
                break;
            default:
                return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CollapseGroups() => CollapseGroupsRequested?.Invoke(this, EventArgs.Empty);
}
