using System.IO.Abstractions;
using AwesomeAssertions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Submodules;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility.Git;
using GitExtensions.Xplat.Core.Plugins;
using GitExtUtils;
using NUnit.Framework;
using ResourceManager;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PluginServicesTests
{
    [TestCase(typeof(IGitExecutorProvider))]
    [TestCase(typeof(ISubmoduleStatusProvider))]
    [TestCase(typeof(IGitBranchNameNormaliser))]
    [TestCase(typeof(IFileSystem))]
    [TestCase(typeof(IGitDirectoryResolver))]
    [TestCase(typeof(IRepositoryDescriptionProvider))]
    [TestCase(typeof(IAppTitleGenerator))]
    [TestCase(typeof(ILinkFactory))]
    [TestCase(typeof(ISubscribableTraceListener))]
    public void Container_should_have_upstreams_services_without_WinForms(Type service)
        => PluginServices.Container.GetService(service).Should().BeAssignableTo(service);

    [Test]
    public void Container_should_be_one_for_the_app()
        => PluginServices.Container.Should().BeSameAs(PluginServices.Container);
}
