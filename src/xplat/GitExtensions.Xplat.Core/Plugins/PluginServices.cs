using System.ComponentModel.Design;
using System.IO.Abstractions;
using GitCommands;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitExtUtils;
using ResourceManager;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  Upstream's service container as plugins reach it through <c>IGitUICommands.GetService</c>: the services the app's
///  <c>GitExtensions.ServiceContainerRegistry</c> registers (reimplemented, as that class is internal to upstream's exe) and
///  those of <c>GitExtUtils</c> and <c>GitCommands</c> (their <c>ServiceContainerRegistry</c>, used as is). <c>GitUI</c>'s
///  services are WinForms ones (jump lists, prompts, hotkey settings, consoles) and are not offered.
/// </summary>
public static class PluginServices
{
    // One container for the app, as upstream's: GitExtUtils's trace listener adds itself to the trace listeners.
    private static readonly Lazy<ServiceContainer> _container = new(Create);

    /// <summary>
    ///  The app's container.
    /// </summary>
    public static IServiceProvider Container => _container.Value;

    private static ServiceContainer Create()
    {
        ServiceContainer container = new();
        GitExtUtils.ServiceContainerRegistry.RegisterServices(container);

        FileSystem fileSystem = new();
        GitDirectoryResolver gitDirectoryResolver = new(fileSystem);
        RepositoryDescriptionProvider repositoryDescriptionProvider = new(gitDirectoryResolver);
        container.AddService<IFileSystem>(fileSystem);
        container.AddService<IGitDirectoryResolver>(gitDirectoryResolver);
        container.AddService<IRepositoryDescriptionProvider>(repositoryDescriptionProvider);
        container.AddService<IAppTitleGenerator>(new AppTitleGenerator(repositoryDescriptionProvider));
        container.AddService<ILinkFactory>(new LinkFactory());

        GitCommands.ServiceContainerRegistry.RegisterServices(container);
        return container;
    }
}
