using Avalonia.Controls;
using Avalonia.Media;
using GitExtensions.Xplat.Core.Operations;
using GitExtensions.Xplat.Ui;
using GitExtUtils.GitUI.Theming;

namespace GitExtensions.Xplat.App;

/// <summary>
///  Upstream's <c>CommitSummaryUserControl</c> (texts in <see cref="CreateBranchDialog.Summarize"/>), shared by the create
///  branch and reset current branch windows.
/// </summary>
public partial class CommitSummaryView : UserControl
{
    private const string Unknown = "---";

    // CommitSummaryUserControl's label colors.
    private static readonly System.Drawing.Color _tagsBackColor = System.Drawing.Color.LightSteelBlue;
    private static readonly System.Drawing.Color _branchesBackColor = System.Drawing.Color.LightSalmon;

    public CommitSummaryView()
    {
        InitializeComponent();
        AuthorCaption.Text = ResourceManager.TranslatedStrings.Author + ":";
        DateCaption.Text = ResourceManager.TranslatedStrings.CommitDate + ":";
    }

    /// <summary>
    ///  Shows <paramref name="summary"/>, or upstream's "No revision" when it is null.
    /// </summary>
    public void Show(CommitSummary? summary)
    {
        string notAvailable = UpstreamTranslation.ByText(CreateBranchDialog.NotAvailable);
        SummaryGroup.Header = summary?.ShortId ?? UpstreamTranslation.ByText(CreateBranchDialog.NoRevision);
        AuthorText.Text = summary?.Author ?? Unknown;
        DateText.Text = summary?.Date ?? Unknown;
        MessageText.Text = summary?.Subject ?? Unknown;
        ShowRefs(TagsBorder, TagsText, summary?.Tags, _tagsBackColor);
        ShowRefs(BranchesBorder, BranchesText, summary?.Branches, _branchesBackColor);

        // Upstream colors and bolds the tags and branches when there are some, else shows "n/a".
        void ShowRefs(Border border, TextBlock text, string? refs, System.Drawing.Color color)
        {
            bool any = !string.IsNullOrEmpty(refs);
            text.Text = refs is null ? Unknown : any ? refs : notAvailable;
            text.FontWeight = any ? FontWeight.Bold : FontWeight.Normal;
            System.Drawing.Color back = color.AdaptBackColor();
            if (any)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(back.R, back.G, back.B));
                text.Foreground = Brushes.Black;
            }
            else
            {
                // Cleared rather than set to null, which would leave the text without a brush instead of inheriting one.
                border.ClearValue(Border.BackgroundProperty);
                text.ClearValue(TextBlock.ForegroundProperty);
            }
        }
    }
}
