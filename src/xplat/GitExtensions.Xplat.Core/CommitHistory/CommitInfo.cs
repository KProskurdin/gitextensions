using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Gpg;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Repository;
using GitExtensions.Xplat.Core.Settings;
using GitExtUtils;
using GitUIPluginInterfaces;
using ResourceManager;
using ResourceManager.CommitDataRenders;

namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  A run of upstream's commit info text: plain, underlined (an annotated tag's name), or a link. A link is an address to
///  open (<c>mailto:</c>, <c>https:</c>) or one of upstream's internal <c>gitext://</c> commands, which
///  <see cref="CommitInfoText.TryParseCommand"/> reads.
/// </summary>
public sealed record InfoSpan(string Text, string? Link = null, bool Underline = false);

/// <summary>
///  A line of upstream's commit info header: the label, such as "Author:", and its value.
/// </summary>
public sealed record InfoHeaderRow(string Label, IReadOnlyList<InfoSpan> Value);

/// <summary>
///  Upstream's GPG tab (<c>RevisionGpgInfoControl</c>): the commit's signature and the signature of its tags, each with the
///  name of upstream's status image, or null when there is none. <see cref="TagText"/> is null when the commit has no tag,
///  and upstream then hides the tag row.
/// </summary>
public sealed record GpgDisplay(string? CommitImage, string CommitText, string? TagImage, string? TagText);

/// <summary>
///  Upstream's commit info texts. Upstream renders them as XHTML into rich text boxes (<c>CommitDataHeaderRenderer</c>,
///  <c>CommitDataBodyRenderer</c>, <c>RefsFormatter</c>, with <c>LinkFactory</c>'s links); the new shell renders the same
///  markup with upstream's renderers and reads it back as lines of <see cref="InfoSpan"/>s.
/// </summary>
public static partial class CommitInfoText
{
    /// <summary>
    ///  Upstream's <c>LinkFactory</c> internal link scheme (<c>gitext://gotocommit/&lt;hash&gt;</c>,
    ///  <c>gotobranch</c>, <c>gototag</c>, <c>showall</c>).
    /// </summary>
    public const string InternalScheme = "gitext";

    private static readonly CommitDataHeaderRenderer _headerRenderer =
        new(new TabLabelFormatter(), new DateFormatter(), new HeaderStyle(), new LinkFactory());

    [GeneratedRegex(@"<(?<close>/?)(?<tag>[a-zA-Z]+)(?<attributes>[^>]*)>", RegexOptions.ExplicitCapture)]
    private static partial Regex TagRegex { get; }

    [GeneratedRegex("""href\s*=\s*(?:'(?<uri>[^']*)'|"(?<uri>[^"]*)")""", RegexOptions.ExplicitCapture)]
    private static partial Regex HrefRegex { get; }

    /// <summary>
    ///  Reads upstream's commit info markup (text with <c>&lt;a href&gt;</c> links and <c>&lt;u&gt;</c>, HTML-encoded) as
    ///  lines of spans.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<InfoSpan>> Parse(string markup)
    {
        List<IReadOnlyList<InfoSpan>> lines = [];
        List<InfoSpan> line = [];
        string? link = null;
        int underline = 0;
        int position = 0;
        foreach (Match match in TagRegex.Matches(markup))
        {
            AddText(markup[position..match.Index]);
            position = match.Index + match.Length;
            bool close = match.Groups["close"].Length > 0;
            switch (match.Groups["tag"].Value.ToLowerInvariant())
            {
                case "a":
                    link = close
                        ? null
                        : WebUtility.HtmlDecode(HrefRegex.Match(match.Groups["attributes"].Value).Groups["uri"].Value);
                    break;
                case "u":
                    underline = Math.Max(0, underline + (close ? -1 : 1));
                    break;
                case "br":
                    NewLine();
                    break;
            }
        }

        AddText(markup[position..]);
        lines.Add(line);
        return lines;

        void AddText(string encoded)
        {
            string[] parts = WebUtility.HtmlDecode(encoded).Replace("\r", "").Split('\n');
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    NewLine();
                }

                if (parts[i].Length > 0)
                {
                    line.Add(new InfoSpan(parts[i], link, underline > 0));
                }
            }
        }

        void NewLine()
        {
            lines.Add(line);
            line = [];
        }
    }

    /// <summary>
    ///  Upstream's <c>CommitInfoHeader</c>: author, dates, committer, hash, and the children and parents as links that select
    ///  them.
    /// </summary>
    public static IReadOnlyList<InfoHeaderRow> Header(CommitData data)
    {
        string markup = _headerRenderer.Render(data, showRevisionsAsLinks: true);
        List<InfoHeaderRow> rows = [];
        foreach (string line in markup.Replace("\r", "").Split('\n'))
        {
            int tab = line.IndexOf('\t');
            string label = tab < 0 ? "" : WebUtility.HtmlDecode(line[..tab]);
            rows.Add(new InfoHeaderRow(label, [.. Parse(line[(tab + 1)..]).SelectMany(spans => spans)]));
        }

        return rows;
    }

    /// <summary>
    ///  Upstream's "Copy commit info" text of the header: without the relative dates, the children and the parents.
    /// </summary>
    public static string HeaderPlainText(IReadOnlyList<InfoHeaderRow> rows)
        => _headerRenderer.GetPlainText(string.Join("\n", rows.Select(row => $"{row.Label}\t{PlainText(row.Value)}")));

    public static string PlainText(IEnumerable<InfoSpan> spans) => string.Concat(spans.Select(span => span.Text));

    public static string PlainText(IReadOnlyList<IReadOnlyList<InfoSpan>> lines)
        => string.Join(Environment.NewLine, lines.Select(PlainText));

    /// <summary>
    ///  Reads one of upstream's internal links: <paramref name="command"/> is <c>gotocommit</c>, <c>gotobranch</c>,
    ///  <c>gototag</c> or <c>showall</c>, and <paramref name="data"/> its argument.
    /// </summary>
    public static bool TryParseCommand(string? link, out string command, out string data)
    {
        command = "";
        data = "";
        if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) || uri.Scheme != InternalScheme)
        {
            return false;
        }

        command = uri.Host;
        data = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        return true;
    }

    // Upstream's TabbedHeaderLabelFormatter pads the label with tabs to line up the values; the shell puts the values in a
    // column instead, so one tab separates them.
    private sealed class TabLabelFormatter : IHeaderLabelFormatter
    {
        public string FormatLabel(string label, int desiredLength) => WebUtility.HtmlEncode(label) + ":\t";
    }

    private sealed class HeaderStyle : IHeaderRenderStyleProvider
    {
        public Font GetFont(Graphics g) => throw new NotSupportedException();

        public int GetMaxWidth() => 16;

        public IEnumerable<int> GetTabStops() => [];
    }
}

/// <summary>
///  Reads what upstream's commit info shows below the message (<c>CommitInfo.UpdateRevisionInfo</c>), and its GPG tab.
/// </summary>
public sealed class CommitInfoReader
{
    // Upstream's RefsFormatter: a long list shows 10 refs and a "Show all" link.
    private const int MaximumDisplayedLinesIfLimited = 12;
    private const int MaximumDisplayedRefsIfLimited = MaximumDisplayedLinesIfLimited - 2;
    private const string RemotesPrefix = "remotes/";

    // Upstream's CommitInfo and GitUI TranslatedStrings texts.
    private const string RelatedLinks = "Related links:";
    private const string DerivesFromTag = "Derives from tag:";
    private const string DerivesFromNoTag = "Derives from no tag";
    private const string PlusCommits = "commits";
    private const string ContainedInBranches = "Contained in branches:";
    private const string ContainedInNoBranch = "Contained in no branch";
    private const string ContainedInTags = "Contained in tags:";
    private const string ContainedInNoTag = "Contained in no tag";

    // Upstream's RevisionGpgInfoControl texts.
    private const string CommitNotSigned = "Commit is not signed";
    private const string TagNotSigned = "Tag is not signed";

    private readonly LinkFactory _linkFactory = new();

    /// <summary>
    ///  The annotated tags' messages, the related links, the branches and tags that contain the commit, and the tag it
    ///  derives from, as the request's options ask, separated by empty lines.
    /// </summary>
    public Task<IReadOnlyList<IReadOnlyList<InfoSpan>>> LoadAsync(CommitInfoRequest request,
        CancellationToken cancellationToken = default)
        => Task.Run(() => Load(request, cancellationToken), cancellationToken);

    /// <summary>
    ///  Upstream's GPG tab (<c>GpgInfoProvider</c> and <c>RevisionGpgInfoControl</c>), or null for an artificial commit.
    /// </summary>
    public static Task<GpgDisplay?> LoadGpgAsync(string repositoryPath, string hash)
        => Task.Run(async () =>
        {
            if (ArtificialCommits.IsArtificial(hash))
            {
                return null;
            }

            GitModule module = CreateModule(repositoryPath);
            ObjectId objectId = ObjectId.Parse(hash);
            GitRevision revision = new(objectId) { Refs = TagsAt(module, objectId) };
            GpgInfo? info = await new GpgInfoProvider(new GitGpgController(() => module)).LoadGpgInfoAsync(revision);
            return Display(info);
        });

    /// <summary>
    ///  Upstream's <c>RevisionGpgInfoControl.DisplayGpgInfo</c>.
    /// </summary>
    public static GpgDisplay Display(GpgInfo? info)
    {
        if (info is null)
        {
            return new GpgDisplay(null, CommitNotSigned, null, null);
        }

        string? commitImage = info.CommitStatus switch
        {
            CommitStatus.GoodSignature => "CommitSignatureOk",
            CommitStatus.MissingPublicKey => "CommitSignatureWarning",
            CommitStatus.SignatureError => "CommitSignatureError",
            _ => null,
        };
        string commitText = info.CommitStatus != CommitStatus.NoSignature
            ? Lines(info.CommitVerificationMessage)
            : CommitNotSigned;
        string? tagImage = info.TagStatus switch
        {
            TagStatus.OneGood => "TagOk",
            TagStatus.OneBad => "TagError",
            TagStatus.Many => "TagMany",
            TagStatus.NoPubKey => "TagWarning",
            _ => null,
        };
        string? tagText = info.TagStatus switch
        {
            TagStatus.NoTag => null,
            TagStatus.TagNotSigned => TagNotSigned,
            _ => Lines(info.TagVerificationMessage ?? ""),
        };
        return new GpgDisplay(commitImage, commitText, tagImage, tagText);

        static string Lines(string text) => text.Replace("\r\n", "\n").TrimEnd();
    }

    private IReadOnlyList<IReadOnlyList<InfoSpan>> Load(CommitInfoRequest request, CancellationToken cancellationToken)
    {
        // As upstream, an artificial commit has none of it.
        if (ArtificialCommits.IsArtificial(request.Hash))
        {
            return [];
        }

        CommitInfoOptions options = request.Options;
        GitModule module = CreateModule(request.RepositoryPath);
        ObjectId objectId = ObjectId.Parse(request.Hash);
        IReadOnlyDictionary<string, int> tagOrder = SortedTags(module);
        List<string> sections = [];

        if (options.AnnotatedTagsMessages)
        {
            sections.Add(AnnotatedTagsInfo(module, objectId, tagOrder, cancellationToken));
        }

        sections.Add(LinksInfo(request.Links));
        cancellationToken.ThrowIfCancellationRequested();

        if (options.Branches)
        {
            bool getLocal = options.BranchesLocal || options.BranchesRemoteIfNoLocal;
            bool getRemote = options.BranchesRemote || options.BranchesRemoteIfNoLocal;
            List<string> branches =
                [.. module.GetAllBranchesWhichContainGivenCommit(objectId, getLocal, getRemote, cancellationToken)];
            branches.Sort(new BranchComparer(branches, module.GetSelectedBranch(), request.Sorting));
            sections.Add(FormatBranches(branches, options, limit: !request.ShowAllBranches));
        }

        if (options.Tags)
        {
            IEnumerable<string> tags = module.GetAllTagsWhichContainGivenCommit(objectId, cancellationToken)
                .OrderBy(tag => TagIndex(tagOrder, tag));
            sections.Add(FormatTags([.. tags], limit: !request.ShowAllTags));
        }

        if (options.DerivesFromTag)
        {
            sections.Add(DescribeInfo(module, objectId, cancellationToken));
        }

        string body = string.Join("\n\n", sections.Where(section => section.Length > 0));
        return body.Length == 0 ? [] : CommitInfoText.Parse(body);
    }

    private static string AnnotatedTagsInfo(GitModule module, ObjectId objectId,
        IReadOnlyDictionary<string, int> tagOrder,
        CancellationToken cancellationToken)
    {
        // Upstream lists the dereferenced refs of the annotated tags (refs/tags/name^{}), whose id is the commit's.
        IEnumerable<string> lines = TagsAt(module, objectId)
            .Where(reference => reference.IsDereference)
            .Select(reference => reference.LocalName)
            .Distinct()
            .OrderBy(tag => TagIndex(tagOrder, tag))
            .Select(tag => (Tag: tag, Message: module.GetTagMessage(tag, cancellationToken)))
            .Where(tag => tag.Message is not null)
            .Select(tag => $"<u>{tag.Tag}</u>: {WebUtility.HtmlEncode(tag.Message)}");
        return string.Join("\n", lines).TrimEnd();
    }

    // Upstream's CommitInfo "Related links", which GitCommitHistory reads with the details.
    private string LinksInfo(IReadOnlyList<RevisionLink> links)
        => links.Count == 0
            ? ""
            : $"{WebUtility.HtmlEncode(RelatedLinks)} {string.Join(", ", links.Select(link => _linkFactory.CreateLink(link.Caption, link.Uri)))}";

    private string DescribeInfo(GitModule module, ObjectId objectId, CancellationToken cancellationToken)
    {
        (string precedingTag, string commitCount) =
            new GitDescribeProvider(() => module).Get(objectId, cancellationToken);
        if (string.IsNullOrEmpty(precedingTag))
        {
            return WebUtility.HtmlEncode(DerivesFromNoTag);
        }

        string info = $"{WebUtility.HtmlEncode(DerivesFromTag)} {_linkFactory.CreateTagLink(precedingTag)}";
        return string.IsNullOrEmpty(commitCount)
            ? info
            : $"{info} + {commitCount} {WebUtility.HtmlEncode(PlusCommits)}";
    }

    // Upstream's RefsFormatter.FilterAndFormatBranches: "git branch -a" names remote branches "remotes/<remote>/<name>";
    // with "remote branches only when no local branch contains it", the remote ones are dropped after the first local one.
    private string FormatBranches(IReadOnlyList<string> branches, CommitInfoOptions options, bool limit)
    {
        List<string> formatted = [];
        bool truncated = false;
        bool getLocal = options.BranchesLocal || options.BranchesRemoteIfNoLocal;
        bool getRemote = options.BranchesRemote || options.BranchesRemoteIfNoLocal;
        bool allowRemote = getRemote;
        foreach (string branch in branches)
        {
            string name = branch;
            bool isLocal;
            if (getLocal && getRemote)
            {
                isLocal = !branch.StartsWith(RemotesPrefix, StringComparison.Ordinal);
                if (!isLocal)
                {
                    name = branch[RemotesPrefix.Length..];
                }
            }
            else
            {
                isLocal = !getRemote;
            }

            if ((isLocal && options.BranchesLocal) || (!isLocal && allowRemote))
            {
                if (limit && formatted.Count == MaximumDisplayedLinesIfLimited)
                {
                    formatted.RemoveRange(MaximumDisplayedRefsIfLimited,
                        MaximumDisplayedLinesIfLimited - MaximumDisplayedRefsIfLimited);
                    truncated = true;
                    break;
                }

                formatted.Add(_linkFactory.CreateBranchLink(name));
            }

            if (isLocal && options.BranchesRemoteIfNoLocal)
            {
                allowRemote = false;
            }
        }

        return Format(formatted, ContainedInBranches, ContainedInNoBranch, "branches", truncated);
    }

    private string FormatTags(IReadOnlyList<string> tags, bool limit)
    {
        bool truncated = limit && tags.Count > MaximumDisplayedLinesIfLimited;
        IEnumerable<string> shown = truncated ? tags.Take(MaximumDisplayedRefsIfLimited) : tags;
        return Format([.. shown.Select(_linkFactory.CreateTagLink)], ContainedInTags, ContainedInNoTag, "tags",
            truncated);
    }

    private string Format(IReadOnlyList<string> links, string prefix, string textIfEmpty, string what, bool truncated)
    {
        if (links.Count == 0)
        {
            return WebUtility.HtmlEncode(textIfEmpty);
        }

        string text = $"{WebUtility.HtmlEncode(prefix)}\n{string.Join("\n", links)}";
        return truncated ? $"{text}\n{_linkFactory.CreateShowAllLink(what)}" : text;
    }

    // Upstream's CommitInfo.GetSortedTags: the tags, newest first.
    private static IReadOnlyDictionary<string, int> SortedTags(GitModule module)
    {
        GitArgumentBuilder args = new("for-each-ref")
        {
            @"--sort=""-taggerdate""", @"--format=""%(refname)""", "refs/tags/"
        };
        Dictionary<string, int> order = new(StringComparer.Ordinal);
        foreach (string name in module.GitExecutable.GetOutput(args).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            order.TryAdd(name.Trim(), order.Count);
        }

        return order;
    }

    // Upstream's CommitInfo.TagsComparer: a tag's place in the newest-first list, -1 when it is not there.
    private static int TagIndex(IReadOnlyDictionary<string, int> order, string tag)
        => order.TryGetValue(
            tag.StartsWith(RemotesPrefix, StringComparison.Ordinal) ? $"refs/{tag}" : $"refs/tags/{tag}",
            out int index)
            ? index
            : -1;

    private static IReadOnlyList<IGitRef> TagsAt(GitModule module, ObjectId objectId)
        => [.. module.GetRefs(RefsFilter.Tags).Where(reference => reference.ObjectId == objectId)];

    private static GitModule CreateModule(string path)
        => new(new GitExecutorProvider(new GitDirectoryResolver()), path);

    /// <summary>
    ///  Upstream's <c>CommitInfo.BranchComparer</c>: the current branch (or the detached HEAD) first, then the prioritized
    ///  local branches in priority order, the prioritized remote branches grouped by branch priority and then by remote
    ///  priority, the other local branches and last the other remote branches; equal places by name.
    /// </summary>
    internal sealed class BranchComparer : IComparer<string>
    {
        private readonly Dictionary<string, int> _orderByBranch = new(StringComparer.Ordinal);

        public BranchComparer(IEnumerable<string> branches, string currentBranch, RefSorting sorting)
        {
            bool isDetachedHead = DetachedHeadParser.IsDetachedHead(currentBranch);
            string[] branchRegexes = Split(sorting.PrioritizedBranchNames);
            string[] localBranchRegexes = [.. branchRegexes.Select(regex => $"^({regex})$")];
            string[] remoteBranchRegexes = [.. branchRegexes.Select(regex => $"^{RemotesPrefix}[^/]+/({regex})$")];
            string[] remoteRegexes =
                [.. Split(sorting.PrioritizedRemoteNames).Select(regex => $"^{RemotesPrefix}({regex})/")];

            // The groups of a prioritized remote branch: the prioritized remotes and one for the others.
            int remotesGroupLength = remoteRegexes.Length + 1;
            int prioritizedRemoteBranchesLength = remoteBranchRegexes.Length * remotesGroupLength;
            foreach (string branch in branches)
            {
                _orderByBranch[branch] = Order(branch);
            }

            return;

            int Order(string branch)
            {
                if (isDetachedHead ? DetachedHeadParser.IsDetachedHead(branch) : branch == currentBranch)
                {
                    return 0;
                }

                int order = 1;
                if (!branch.StartsWith(RemotesPrefix, StringComparison.Ordinal))
                {
                    // Local branches that are not prioritized come after the prioritized remote branches.
                    if (!TryGetOrder(branch, localBranchRegexes, out int localOrder))
                    {
                        order += prioritizedRemoteBranchesLength;
                    }

                    return order + localOrder;
                }

                order += localBranchRegexes.Length;
                if (!TryGetOrder(branch, remoteBranchRegexes, out int remoteBranchOrder))
                {
                    // After the local branches that are not prioritized.
                    order += 1;
                }

                TryGetOrder(branch, remoteRegexes, out int remoteOrder);
                return order + (remoteBranchOrder * remotesGroupLength) + remoteOrder;
            }

            static bool TryGetOrder(string branch, string[] regexes, out int order)
            {
                for (order = 0; order < regexes.Length; order++)
                {
                    if (Regex.IsMatch(branch, regexes[order], RegexOptions.ExplicitCapture))
                    {
                        return true;
                    }
                }

                return false;
            }

            static string[] Split(string names)
                => names.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        public int Compare(string? a, string? b)
        {
            if (b is null)
            {
                return -1;
            }

            if (a is null)
            {
                return 1;
            }

            int priorityA = _orderByBranch[a];
            int priorityB = _orderByBranch[b];
            return priorityA == priorityB ? StringComparer.Ordinal.Compare(a, b) : priorityA - priorityB;
        }
    }
}

/// <summary>
///  What <see cref="CommitInfoReader.LoadAsync"/> reads for the commit <paramref name="Hash"/>: what
///  <paramref name="Options"/> asks, the related <paramref name="Links"/> read with the details, and branches ordered with
///  the priorities of <paramref name="Sorting"/>. Long branch and tag lists end with a "Show all" link unless
///  <paramref name="ShowAllBranches"/> or <paramref name="ShowAllTags"/> is set.
/// </summary>
public sealed record CommitInfoRequest(
    string RepositoryPath,
    string Hash,
    CommitInfoOptions Options,
    RefSorting Sorting,
    IReadOnlyList<RevisionLink> Links,
    bool ShowAllBranches = false,
    bool ShowAllTags = false);
