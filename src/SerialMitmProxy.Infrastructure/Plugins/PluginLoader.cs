using System.Reflection;
using SerialMitmProxy.Core.Abstractions;

namespace SerialMitmProxy.Infrastructure.Plugins;

public static class PluginLoader
{
    public static IReadOnlyList<IFramePlugin> Load(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return Array.Empty<IFramePlugin>();
        }

        var plugins = new List<IFramePlugin>();
        foreach (var dllPath in Directory.EnumerateFiles(folderPath, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var assembly = Assembly.LoadFrom(dllPath);
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (!typeof(IFramePlugin).IsAssignableFrom(type))
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is IFramePlugin plugin)
                {
                    plugins.Add(plugin);
                }
            }
        }

        return plugins;
    }
}
