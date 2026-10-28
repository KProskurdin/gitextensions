using AwesomeAssertions;
using GitExtensions.Xplat.Core.CommitHistory;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class NavigationHistoryTests
{
    [Test]
    public void NavigateBackward_and_NavigateForward_should_walk_the_selected_commits()
    {
        NavigationHistory history = new();
        history.Push("a");
        history.Push("b");
        history.Push("b");
        history.Push("c");

        history.NavigateBackward().Should().Be("b");
        history.NavigateBackward().Should().Be("a");
        history.NavigateBackward().Should().BeNull("there is nothing before the first commit");
        history.NavigateForward().Should().Be("b");
        history.NavigateForward().Should().Be("c");
        history.NavigateForward().Should().BeNull();
    }

    [Test]
    public void Push_should_forget_the_commits_navigated_back_from()
    {
        NavigationHistory history = new();
        history.Push("a");
        history.Push("b");
        history.NavigateBackward();

        history.Push("c");

        history.CanNavigateForward.Should().BeFalse();
        history.NavigateBackward().Should().Be("a");
    }
}
