using System.Reflection;
using AwesomeAssertions;
using GitExtensions.Xplat.Core.Plugins;
using NUnit.Framework;

namespace GitExtensions.Xplat.Core.Tests;

internal sealed class PluginAssemblyResolveTests
{
    [Test]
    public void UpstreamHandler_should_find_upstreams_assembly_resolve_handler()
    {
        ResolveEventHandler handler = PluginAssemblyResolve.UpstreamHandler();

        handler.Method.Name.Should().Be("CurrentDomain_AssemblyResolve");
    }

    [Test]
    public void Resolve_should_skip_satellite_assembly_requests()
    {
        int calls = 0;
        ResolveEventHandler upstream = (_, _) =>
        {
            calls++;
            return typeof(PluginAssemblyResolveTests).Assembly;
        };

        Assembly? assembly = PluginAssemblyResolve.Resolve(upstream, null,
            new ResolveEventArgs("Microsoft.VisualStudio.Composition.resources, Version=17.0.0.0, Culture=ru-RU"));

        assembly.Should().BeNull();
        calls.Should().Be(0);
    }

    [Test]
    public void Resolve_should_not_reenter_upstream_from_inside_its_own_scan()
    {
        int calls = 0;
        Assembly? inner = typeof(object).Assembly;

        Assembly? assembly = PluginAssemblyResolve.Resolve(Upstream, null, new ResolveEventArgs("Plugin.Dependency"));

        assembly.Should().BeSameAs(typeof(PluginAssemblyResolveTests).Assembly);
        inner.Should().BeNull();
        calls.Should().Be(1);
        return;

        Assembly? Upstream(object? sender, ResolveEventArgs args)
        {
            calls++;
            inner = PluginAssemblyResolve.Resolve(Upstream, sender, new ResolveEventArgs("Nested.Dependency"));
            return typeof(PluginAssemblyResolveTests).Assembly;
        }
    }

    [Test]
    public void Resolve_should_run_upstream_again_after_a_resolve_completes()
    {
        int calls = 0;
        ResolveEventHandler upstream = (_, _) =>
        {
            calls++;
            return null;
        };

        PluginAssemblyResolve.Resolve(upstream, null, new ResolveEventArgs("First.Dependency"));
        PluginAssemblyResolve.Resolve(upstream, null, new ResolveEventArgs("Second.Dependency"));

        calls.Should().Be(2);
    }
}
