using BugReporter.Serialization;

namespace GitExtensions.Xplat.Core.Diagnostics;

/// <summary>
///  An exception in the bug report's tree (upstream's <c>ExceptionDetails</c> tree view): its type, and its inner
///  exceptions under it.
/// </summary>
public sealed record ExceptionNode(string? Type, SerializableException Exception, IReadOnlyList<ExceptionNode> Children);

/// <summary>
///  A row of the bug report's property list (upstream's <c>ExceptionDetails</c> list view). Upstream shows the extended
///  information rows in bold.
/// </summary>
public sealed record ExceptionProperty(string Property, string Information, bool IsExtended = false);

/// <summary>
///  Upstream's <c>ExceptionDetails</c> without its WinForms controls: the exceptions as a tree and the properties of each.
/// </summary>
public static class ExceptionDetails
{
    /// <summary>
    ///  The tree of <paramref name="exception"/>: the exception, under it its inner exception and the exceptions an
    ///  aggregate holds, each with its own. Upstream puts every deeper inner exception under the first child of the root
    ///  (its <c>FillInnerExceptionTree</c> always passes <c>Nodes[0]</c>); here each is under the exception it is inside.
    /// </summary>
    public static ExceptionNode Tree(SerializableException exception)
    {
        List<ExceptionNode> children = [];
        if (exception.InnerException is { } inner)
        {
            children.Add(Tree(inner));
        }

        if (exception.InnerExceptions is { } inners)
        {
            children.AddRange(inners.Select(Tree));
        }

        return new ExceptionNode(exception.Type, exception, children);
    }

    /// <summary>
    ///  Upstream's <c>DisplayExceptionDetails</c>: the rows of <paramref name="exception"/>, in upstream's order.
    /// </summary>
    public static IReadOnlyList<ExceptionProperty> Properties(SerializableException exception)
    {
        List<ExceptionProperty> rows = [];
        Add("Exception", exception.Type);
        Add("Message", exception.Message);
        Add("Target Site", exception.TargetSite);
        Add("Inner Exception", exception.InnerException?.Type);
        Add("Source", exception.Source);
        Add("Help Link", exception.HelpLink);
        Add("Stack Trace", exception.StackTrace);
        if (exception.Data is not null)
        {
            foreach (KeyValuePair<object, object> pair in exception.Data)
            {
                rows.Add(new ExceptionProperty($"Data[\"{pair.Key}\"]", pair.Value.ToString() ?? ""));
            }
        }

        if (exception.ExtendedInformation is not null)
        {
            foreach (KeyValuePair<string, object> info in exception.ExtendedInformation)
            {
                rows.Add(new ExceptionProperty(info.Key, info.Value.ToString() ?? "", IsExtended: true));
            }
        }

        return rows;

        void Add(string property, string? information)
        {
            if (information is not null)
            {
                rows.Add(new ExceptionProperty(property, information));
            }
        }
    }
}
