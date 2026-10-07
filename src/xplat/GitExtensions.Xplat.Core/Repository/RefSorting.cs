using System.Text.RegularExpressions;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Repository;

/// <summary>
///  How the branch list is sorted: upstream's <c>RefsSortBy</c> and <c>RefsSortOrder</c>, which git applies
///  (<c>for-each-ref --sort</c>), and upstream's <c>PrioritizedBranchNames</c> and <c>PrioritizedRemoteNames</c>, which move
///  matching branches and remotes to the top of the left panel.
/// </summary>
public sealed record RefSorting(
    GitRefsSortBy SortBy = GitRefsSortBy.Default,
    GitRefsSortOrder Order = GitRefsSortOrder.Descending,
    string PrioritizedBranchNames = "",
    string PrioritizedRemoteNames = "")
{
    /// <summary>
    ///  Upstream's <c>BaseRefTree.OrderByPriority</c>: the setting holds regular expressions separated by ';', each matched
    ///  against the whole key; items matching an earlier expression come first, items matching none last, and the order
    ///  is otherwise kept. An expression that is not valid matches nothing (upstream would fail to fill the tree).
    /// </summary>
    public static IReadOnlyList<T> OrderByPriority<T>(IReadOnlyList<T> items, Func<T, string> key, string setting)
    {
        Regex?[] expressions =
        [
            .. setting.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Compile),
        ];
        if (expressions.Length == 0)
        {
            return items;
        }

        return [.. items.OrderBy(item => Priority(key(item)))];

        int Priority(string value)
        {
            for (int i = 0; i < expressions.Length; i++)
            {
                if (expressions[i]?.IsMatch(value) is true)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        static Regex? Compile(string expression)
        {
            try
            {
                return new Regex($"^({expression})$", RegexOptions.ExplicitCapture);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
