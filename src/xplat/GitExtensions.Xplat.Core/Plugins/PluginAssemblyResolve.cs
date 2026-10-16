using System.Reflection;
using GitUIPluginInterfaces;

namespace GitExtensions.Xplat.Core.Plugins;

/// <summary>
///  Wraps upstream's <see cref="ManagedExtensibility"/> assembly resolve handler so it cannot recurse off Windows.
/// </summary>
/// <remarks>
///  Upstream's handler reads <c>FileVersionInfo</c> of every file next to the requesting assembly. Off Windows that parses
///  each file as a PE image and throws for any other file; the exception message is a resource lookup, which under a
///  non-English UI culture asks for a satellite assembly, which raises the handler again until the stack overflows.
/// </remarks>
public static class PluginAssemblyResolve
{
    private const string UpstreamHandlerName = "CurrentDomain_AssemblyResolve";
    private const string SatelliteSuffix = ".resources";

    [ThreadStatic]
    private static bool _resolving;

    /// <summary>
    ///  Upstream's private handler, as the delegate <see cref="ManagedExtensibility.Initialise"/> subscribes. Throws when
    ///  upstream renames or changes it, so the guard never silently stops applying.
    /// </summary>
    public static ResolveEventHandler UpstreamHandler()
    {
        MethodInfo method = typeof(ManagedExtensibility).GetMethod(UpstreamHandlerName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"{nameof(ManagedExtensibility)}.{UpstreamHandlerName} not found; update the plugin assembly resolve seam.");
        return method.CreateDelegate<ResolveEventHandler>();
    }

    /// <summary>
    ///  Replaces <paramref name="upstream"/>, already subscribed by <see cref="ManagedExtensibility.Initialise"/>, with the
    ///  guarded <see cref="Resolve"/>.
    /// </summary>
    public static void Replace(ResolveEventHandler upstream)
    {
        AppDomain.CurrentDomain.AssemblyResolve -= upstream;
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Resolve(upstream, sender, args);
    }

    /// <summary>
    ///  Runs <paramref name="upstream"/> unless the request is for a satellite assembly or comes from inside its own scan.
    /// </summary>
    public static Assembly? Resolve(ResolveEventHandler upstream, object? sender, ResolveEventArgs args)
    {
        // A satellite has no file of its own name to find, and upstream's prefix match would hand back the main assembly.
        if (_resolving || args.Name.Split(',')[0].EndsWith(SatelliteSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        _resolving = true;
        try
        {
            return upstream(sender, args);
        }
        finally
        {
            _resolving = false;
        }
    }
}
