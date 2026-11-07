using AwesomeAssertions;
using GitExtensions.Xplat.Core.Diff;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class TextSearchTests
{
    private const string Text = "Foo foo_bar\nfoo Foo";

    [Test]
    public void FindNext_should_find_from_the_offset_and_loop_around_as_upstreams_searcher()
    {
        TextSearch search = new(Text) { LookFor = "foo" };

        search.FindNext(1, searchBackward: false, out bool looped).Should().Be((4, 3));
        looped.Should().BeFalse();
        search.FindNext(17, searchBackward: false, out looped).Should().Be((0, 3), "the search starts again at the top");
        looped.Should().BeTrue();
    }

    [Test]
    public void FindNext_should_search_backward_before_the_offset()
    {
        TextSearch search = new(Text) { LookFor = "foo" };

        search.FindNext(12, searchBackward: true, out bool looped).Should().Be((4, 3));
        looped.Should().BeFalse();
        search.FindNext(0, searchBackward: true, out looped).Should().Be((16, 3));
        looped.Should().BeTrue();
    }

    [Test]
    public void FindNext_should_match_case_and_whole_words_when_asked()
    {
        new TextSearch(Text) { LookFor = "Foo", MatchCase = true }.FindNext(1, false, out _).Should().Be((16, 3));
        new TextSearch(Text) { LookFor = "foo", MatchWholeWordOnly = true }.FindNext(1, false, out _).Should()
            .Be((12, 3), "foo_bar is one word");
    }

    [Test]
    public void FindAll_should_list_every_occurrence_in_the_scan_region()
    {
        new TextSearch(Text) { LookFor = "foo" }.FindAll().Should().Equal((0, 3), (4, 3), (12, 3), (16, 3));
        new TextSearch(Text) { LookFor = "foo", ScanRegion = (12, 7) }.FindAll().Should().Equal((12, 3), (16, 3));
        new TextSearch(Text) { LookFor = "" }.FindAll().Should().BeEmpty();
    }
}
