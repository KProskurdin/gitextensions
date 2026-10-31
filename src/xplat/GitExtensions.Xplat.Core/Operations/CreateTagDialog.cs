using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility.Git;

namespace GitExtensions.Xplat.Core.Operations;

/// <summary>
///  Upstream's <c>FormCreateTag</c> and <c>GitTagController</c> without their windows: the kinds of tag in the window's
///  order, which fields each kind uses, the remote a tag is pushed to, and creating the tag with its message file.
/// </summary>
public static class CreateTagDialog
{
    // Upstream's texts.
    public const string MessageCaption = "Tag";
    public const string NoRevisionSelected = "Select 1 revision to create the tag on.";
    public const string PushToCaption = "Push tag to '{0}'";
    public const string Lightweight = "Lightweight tag";
    public const string Annotated = "Annotated tag";
    public const string SignDefault = "Sign with default GPG";
    public const string SignSpecificKey = "Sign with specific GPG";

    // The remote upstream pushes to when the current branch tracks none.
    private const string DefaultRemote = "origin";

    // The file upstream's GitTagController writes the message to, in the work tree, for git's -F.
    private const string TagMessageFileName = "TAGMESSAGE";

    /// <summary>
    ///  The kinds of tag with upstream's texts, in the order of its drop-down.
    /// </summary>
    public static IReadOnlyList<(TagOperation Operation, string Text)> Operations { get; } =
    [
        (TagOperation.Lightweight, Lightweight),
        (TagOperation.Annotate, Annotated),
        (TagOperation.SignWithDefaultKey, SignDefault),
        (TagOperation.SignWithSpecificKey, SignSpecificKey),
    ];

    /// <summary>
    ///  Upstream's <c>AnnotateDropDownChanged</c>: the key id is for a specific GPG key only.
    /// </summary>
    public static bool UsesKeyId(TagOperation operation) => operation == TagOperation.SignWithSpecificKey;

    /// <summary>
    ///  Upstream's <c>AnnotateDropDownChanged</c>: every kind but a lightweight tag takes a message.
    /// </summary>
    public static bool UsesMessage(TagOperation operation) => operation.CanProvideMessage();

    /// <summary>
    ///  Upstream's <c>FormCreateTag_Load</c>: the current branch's remote, else origin.
    /// </summary>
    public static string PushRemote(IGitModule module)
        => module.GetCurrentRemote() is { Length: > 0 } remote ? remote : DefaultRemote;

    /// <summary>
    ///  Upstream's <c>GitTagController.CreateTag</c>: the message written to a file in the work tree for a kind that takes
    ///  one, upstream's <c>Commands.CreateTag</c> run by <paramref name="run"/>, and the file deleted again.
    /// </summary>
    /// <exception cref="ArgumentException">The arguments are not valid for the kind (upstream's checks).</exception>
    public static async Task<bool> CreateAsync(GitModule module, GitCreateTagArgs args, Func<string, Task<bool>> run)
    {
        string? messageFile = null;
        if (args.Operation.CanProvideMessage())
        {
            messageFile = Path.Join(module.WorkingDir, TagMessageFileName);
            await File.WriteAllTextAsync(messageFile, args.TagMessage);
        }

        try
        {
            return await run(Commands.CreateTag(args, messageFile, module.GetPathForGitExecution).Arguments);
        }
        finally
        {
            if (messageFile is not null && File.Exists(messageFile))
            {
                File.Delete(messageFile);
            }
        }
    }
}
