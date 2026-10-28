using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace GitExtensions.Xplat.App;

/// <summary>
///  The developer aid behind <c>XPLAT_SCREENSHOT=&lt;png&gt;</c>: a window saves its content to the file and closes, so a
///  script or CI can check a run without a person at the screen.
/// </summary>
internal static class Screenshot
{
    public const string EnvironmentVariable = "XPLAT_SCREENSHOT";

    /// <summary>
    ///  Names another window to save instead of the browse window: <see cref="CommitWindow"/>, or
    ///  <see cref="SettingsWindow"/>, optionally followed by ":" and a text typed into its search box, or a dialog
    ///  (<see cref="PullWindow"/>, <see cref="PushWindow"/>, <see cref="RemotesWindow"/>, <see cref="StashWindow"/>, <see cref="CreateBranchWindow"/>, <see cref="CheckoutBranchWindow"/>, <see cref="CheckoutRevisionWindow"/>, <see cref="MergeBranchWindow"/>).
    /// </summary>
    public const string WindowEnvironmentVariable = "XPLAT_SCREENSHOT_WINDOW";

    public const string CommitWindow = "commit";

    public const string SettingsWindow = "settings";

    public const string PullWindow = "pull";

    public const string PushWindow = "push";

    public const string RemotesWindow = "remotes";

    public const string StashWindow = "stash";

    public const string CreateBranchWindow = "createbranch";

    public const string CheckoutBranchWindow = "checkout";

    public const string CheckoutRevisionWindow = "checkoutrevision";

    public const string MergeBranchWindow = "merge";

    public const string CloneWindow = "clone";

    public const string RebaseWindow = "rebase";

    public const string ReflogWindow = "reflog";

    public const string ResetCurrentBranchWindow = "reset";

    public const string FileHistoryWindow = "filehistory";

    public const string ConflictsWindow = "conflicts";

    public const string WorktreesWindow = "worktrees";

    public const string CreateWorktreeWindow = "createworktree";

    public const string MergeSubmoduleWindow = "mergesubmodule";

    public const string CommitDiffWindow = "commitdiff";

    public const string RecentRepositoriesSettingsWindow = "recentrepos";

    public const string GoToCommitWindow = "gotocommit";

    /// <summary>
    ///  The file a window about one file shows (the file history), for the screenshot aid.
    /// </summary>
    public const string FileEnvironmentVariable = "XPLAT_SCREENSHOT_FILE";

    public static string? RequestedWindow => Environment.GetEnvironmentVariable(WindowEnvironmentVariable);

    /// <summary>
    ///  The file to save to, or null when the aid is off.
    /// </summary>
    public static string? RequestedFile
        => Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } file ? file : null;

    public static async Task SaveAsync(Window window, string file)
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        using RenderTargetBitmap bitmap = new(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window);
        await using FileStream stream = File.Create(file);
        bitmap.Save(stream);
    }
}
