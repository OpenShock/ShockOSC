using System.Collections.Concurrent;
using System.Reflection;

namespace OpenShock.Desktop.Plugin;

public static class PluginManager
{
    private static readonly ConcurrentDictionary<string, PluginBase?> PluginDict = new ConcurrentDictionary<string, PluginBase?>();

    public static PluginBase[] Plugins => PluginDict.Values.Where(x => x is not null).Select(x => x!).ToArray();

    public static void LoadPlugin(string assemblyPath)
    {
        var assembly = Assembly.Load(assemblyPath);

        Type ipluginType = typeof(PluginBase);
        var pluginTypes = assembly.GetTypes().Where(t => t.IsAssignableFrom(ipluginType)).ToArray();
        if (pluginTypes.Length == 0) throw new Exception("No plugins found in DLL!");
        if (pluginTypes.Length > 1) throw new Exception($"Expected 1 plugin, found {pluginTypes.Length}");

        PluginBase? plugin = (PluginBase?)Activator.CreateInstance(pluginTypes[0]);
        if (plugin is null) throw new Exception("Failed to instanciate plugin!");

        if (!PluginDict.TryAdd(plugin.Name, plugin)) throw new Exception("Plugin already loaded!");
    }

    internal static bool LoadAllPlugins()
    {
        if (!Directory.Exists("Plugins")) return false;

        foreach (var pluginPath in Directory.GetFiles("Plugins"))
        {
            try
            {
                LoadPlugin(pluginPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load plugin: {ex.Message}");
            }
        }

        return true;
    }

    internal static Stream? LoadPluginResource(string path)
    {
        if (String.IsNullOrEmpty(path)) return null;

        var parts = path.Split(['\\', '/'], 2);
        if (parts is not [string pluginName, string resxPath]) return null;

        if (!PluginDict.TryGetValue(pluginName, out PluginBase? plugin) || plugin == null) return null;

        return plugin.WebResourceManager.GetStream(resxPath);
    }
}
