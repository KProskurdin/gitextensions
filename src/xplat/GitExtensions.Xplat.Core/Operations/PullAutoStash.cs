using GitExtensions.Xplat.Core.Settings;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  The answer to upstream's "Apply stashed items to working directory again?": whether to pop the stash, and whether
///  "Don't show again" was checked on an answer (dismissing the question is not one).
/// </summary>
public sealed record StashPopAnswer(bool Pop, bool Remember);

/// <summary>
///  What <see cref="PullAutoStash"/> asks of the repository and the window.
/// </summary>
public sealed record PullAutoStashServices
{
    /// <summary>
    ///  Upstream's check before it stashes: not a bare repository, and tracked files changed (untracked files and
    ///  submodules aside).
    /// </summary>
    public required Func<Task<bool>> HasLocalChanges { get; init; }

    /// <summary>
    ///  Stashes the local changes, with the untracked files when the argument is true; false when it failed.
    /// </summary>
    public required Func<bool, Task<bool>> Stash { get; init; }

    /// <summary>
    ///  Whether a merge, rebase or other operation is left in the middle, as after a pull with conflicts.
    /// </summary>
    public required Func<Task<bool>> IsInTheMiddleOfAction { get; init; }

    public required Func<Task<StashPopAnswer>> AskPop { get; init; }

    /// <summary>
    ///  Pops the stash made before the pull.
    /// </summary>
    public required Func<Task<bool>> Pop { get; init; }
}

/// <summary>
///  Upstream's auto stash around a pull (<c>FormPull.PullChanges</c>, <c>CalculateStashedValue</c> and <c>PopStash</c>):
///  the local changes are stashed before the merge or rebase, and after a pull that went through the stash is popped as
///  <c>AutoPopStashAfterPull</c> says, or after its question. After a failed pull or one left in the middle, the stash
///  stays.
/// </summary>
public sealed class PullAutoStash(IAppPreferences preferences, PullAutoStashServices services)
{
    /// <summary>
    ///  Runs <paramref name="pull"/> for <paramref name="request"/>, stashing around it when the request asks for auto
    ///  stash; the pull gets the request without it, as this stashes instead of git.
    /// </summary>
    public async Task<bool> PullAsync(PullRequest request, Func<PullRequest, Task<bool>> pull)
    {
        if (!request.AutoStash || request.Action == PullAction.FetchOnly)
        {
            return await pull(request);
        }

        bool stashed = false;
        if (await services.HasLocalChanges())
        {
            if (!await services.Stash(preferences.IncludeUntrackedFilesInAutoStash))
            {
                return false;
            }

            stashed = true;
        }

        bool done = await pull(request with { AutoStash = false });
        if (stashed && done && !await services.IsInTheMiddleOfAction() && await ShouldPopAsync())
        {
            await services.Pop();
        }

        return done;
    }

    // Upstream's AutoPopStashAfterPull question; dismissing the dialog is not an answer to remember.
    private async Task<bool> ShouldPopAsync()
    {
        if (preferences.AutoPopStashAfterPull is { } remembered)
        {
            return remembered;
        }

        StashPopAnswer answer = await services.AskPop();
        if (answer.Remember)
        {
            preferences.AutoPopStashAfterPull = answer.Pop;
            preferences.Save();
        }

        return answer.Pop;
    }
}
