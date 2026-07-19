using System.Reflection;
using System.Runtime.Loader;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal sealed class UmbraPluginLoadContext : AssemblyLoadContext
{
    private static readonly string PluginApiAssemblyName = typeof(IUmbraPlugin).Assembly.GetName().Name!;
    private readonly AssemblyDependencyResolver resolver;

    public UmbraPluginLoadContext(string pluginAssemblyPath)
        : base($"Umbra.Plugin.{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // The SDK contract must retain a single identity in the default context.
        if (string.Equals(assemblyName.Name, PluginApiAssemblyName, StringComparison.OrdinalIgnoreCase))
            return null;

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
