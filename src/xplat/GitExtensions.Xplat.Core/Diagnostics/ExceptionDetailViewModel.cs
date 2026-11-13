namespace GitExtensions.Xplat.Core.Diagnostics;

/// <summary>
///  The view model of upstream's <c>ExceptionDetailView</c>: a property of the bug report's exception and its whole value,
///  both read-only.
/// </summary>
public sealed class ExceptionDetailViewModel(string property, string information)
{
    /// <summary>
    ///  The upstream form whose texts the window shows, by their names.
    /// </summary>
    public const string Category = "ExceptionDetailView";

    public string Property { get; } = property;

    public string Information { get; } = information;
}
