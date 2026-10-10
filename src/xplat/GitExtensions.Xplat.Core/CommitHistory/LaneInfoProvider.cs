using System.Text;
using GitCommands;
using ResourceManager;

// Upstream's namespace, so upstream's BranchFinder (linked unchanged) finds its pull request text here.
namespace GitUI.UserControls.RevisionGrid.Graph;

/// <summary>
///  The new shell's version of upstream's <c>LaneInfoProvider</c>: the text of the revision graph's tooltip for a lane of a
///  row: the commit the lane comes from (and the child it leads to), the branch it was committed to (upstream's
///  <see cref="BranchFinder"/>), and its message. Upstream's version reads two texts from GitUI's <c>TranslatedStrings</c>,
///  which the shell does not have; they are repeated here.
/// </summary>
internal sealed class LaneInfoProvider(
    ILaneNodeLocator nodeLocator,
    IGitRevisionSummaryBuilder gitRevisionSummaryBuilder)
{
    private static readonly TranslationString NoInfoText = new("Sorry, this commit seems to be not loaded.");
    private static readonly TranslationString MergedWithText = new(" (merged with {0})");
    internal static readonly TranslationString ByPullRequestText = new(" by pull request {0}");

    // Upstream's TranslatedStrings.Branch and BodyNotLoaded.
    private const string BranchText = "Branch";

    private const string BodyNotLoadedText =
        "\n\nFull message text is not present in older commits.\nSelect this commit to populate the full message.";

    public string GetLaneInfo(int rowIndex, int lane)
    {
        (RevisionGraphRevision? node, bool isAtNode, RevisionGraphRevision? singleChild) =
            nodeLocator.FindPrevNode(rowIndex, lane);
        if (node is null)
        {
            return string.Empty;
        }

        if (node.GitRevision is null)
        {
            return NoInfoText.Text;
        }

        StringBuilder laneInfoText = new();

        if (singleChild is not null)
        {
            laneInfoText.Append(singleChild.Objectid.ToShortString()).Append(": ")
                .AppendLine(singleChild.GitRevision?.Subject).AppendLine("|");
        }

        if (!node.GitRevision.IsArtificial)
        {
            if (isAtNode)
            {
                laneInfoText.Append("* ");
            }

            laneInfoText.AppendLine(node.GitRevision.Guid);

            BranchFinder branch = new(node);
            if (!string.IsNullOrWhiteSpace(branch.CommittedTo))
            {
                laneInfoText.AppendFormat("\n{0}: {1}", BranchText, branch.CommittedTo);
                if (!string.IsNullOrWhiteSpace(branch.MergedWith))
                {
                    laneInfoText.AppendFormat(MergedWithText.Text, branch.MergedWith);
                }
            }

            laneInfoText.AppendLine();
        }

        if (node.GitRevision.Body is not null)
        {
            laneInfoText.Append(gitRevisionSummaryBuilder.BuildSummary(node.GitRevision.Body));
        }
        else
        {
            laneInfoText.Append(node.GitRevision.Subject);
            if (node.GitRevision.HasMultiLineMessage)
            {
                laneInfoText.Append(BodyNotLoadedText);
            }
        }

        return laneInfoText.ToString();
    }
}
