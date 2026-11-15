using GitCommands;
using GitCommands.Utils;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.Core.Settings;

/// <summary>
///  A choice of an upstream settings enum, named by its description as upstream's pages name them.
/// </summary>
public sealed record EnumOption<T>(T Value) where T : struct, Enum
{
    /// <summary>
    ///  Every value of <typeparamref name="T"/>, in declaration order.
    /// </summary>
    public static IReadOnlyList<EnumOption<T>> All { get; } = [.. Enum.GetValues<T>().Select(value => new EnumOption<T>(value))];

    public override string ToString() => Value.GetDescription();
}

/// <summary>
///  A branch name normaliser symbol, named as upstream's Advanced page names it.
/// </summary>
public sealed record NormaliseSymbolOption(string Label, string Symbol)
{
    public override string ToString() => Label;
}

/// <summary>
///  A theme in the list, named as upstream's appearance page names them.
/// </summary>
public sealed record ThemeOption(ThemeId Id)
{
    public override string ToString()
        => Id == ThemeId.WindowsAppColorModeId ? "Follow the operating system"
            : Id == ThemeId.DefaultLight ? "light (default)"
            : Id.IsBuiltin ? Id.Name
            : $"{Id.Name} (user)";
}

/// <summary>
///  One of upstream's default pull actions, named as upstream's General page names it.
/// </summary>
public sealed record PullActionOption(GitPullAction Action, string Text)
{
    public static IReadOnlyList<PullActionOption> All { get; } =
    [
        new(GitPullAction.None, "Open pull dialog"),
        new(GitPullAction.Merge, "Pull - merge"),
        new(GitPullAction.Rebase, "Pull - rebase"),
        new(GitPullAction.Fetch, "Fetch"),
        new(GitPullAction.FetchAll, "Fetch all"),
        new(GitPullAction.FetchPruneAll, "Fetch and prune all"),
    ];

    public override string ToString() => Text;
}

/// <summary>
///  One of upstream's truncation choices, named as upstream's list names them.
/// </summary>
public sealed record TruncatePathOption(TruncatePathMethod Method, string Text)
{
    public static IReadOnlyList<TruncatePathOption> All { get; } =
    [
        new(TruncatePathMethod.None, "None"),
        new(TruncatePathMethod.Compact, "Compact"),
        new(TruncatePathMethod.TrimStart, "Trim start"),
        new(TruncatePathMethod.FileNameOnly, "Filename only"),
    ];

    public override string ToString() => Text;
}

/// <summary>
///  One of upstream's avatar providers, named as upstream's list names them.
/// </summary>
public sealed record AvatarProviderOption(AvatarProvider Provider)
{
    public static IReadOnlyList<AvatarProviderOption> All { get; } =
        [.. Enum.GetValues<AvatarProvider>().Select(provider => new AvatarProviderOption(provider))];

    public override string ToString() => Provider.ToString();
}

/// <summary>
///  One of upstream's fallback avatar styles, named by upstream's descriptions.
/// </summary>
public sealed record AvatarFallbackOption(AvatarFallbackType Fallback)
{
    public static IReadOnlyList<AvatarFallbackOption> All { get; } =
        [.. Enum.GetValues<AvatarFallbackType>().Select(fallback => new AvatarFallbackOption(fallback))];

    // Upstream lists the enum by its Description attribute ("Author initials"), otherwise by name.
    public override string ToString()
        => typeof(AvatarFallbackType).GetField(Fallback.ToString())?
               .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), inherit: false)
               .OfType<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description
           ?? Fallback.ToString();
}
