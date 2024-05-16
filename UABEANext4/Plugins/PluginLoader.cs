using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Util;

namespace UABEANext4.Plugins;
public class PluginLoader
{
    private readonly List<IUavPluginOption> _loadedPlugins = new();

    public bool LoadPlugin(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var plugLoadCtx = new PluginLoadContext(fullPath);
            var asm = plugLoadCtx.LoadAssemblyByPath(fullPath);
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(IUavPluginOption).IsAssignableFrom(type))
                {
                    object? typeInst = Activator.CreateInstance(type);
                    if (typeInst == null)
                        return false;

                    if (typeInst is not IUavPluginOption plugInst)
                        return false;

                    _loadedPlugins.Add(plugInst);
                }
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    public void LoadPluginsInDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string file in Directory.EnumerateFiles(directory, "*.dll"))
        {
            LoadPlugin(file);
        }
    }

    public async Task<List<PluginOptionModePair>> GetPluginsThatSupport(Workspace workspace, List<AssetInst> assets, UavPluginMode mode)
    {
        var options = new List<PluginOptionModePair>();
        foreach (var option in _loadedPlugins)
        {
            var bothOpt = mode & option.Options;
            foreach (var flag in bothOpt.GetUniqueFlags())
            {
                if (flag == UavPluginMode.All)
                {
                    continue;
                }

                var supported = await option.SupportsSelection(workspace, flag, assets);
                if (supported)
                {
                    options.Add(new PluginOptionModePair(option, flag));
                }
            }
        }

        return options;
    }
}
