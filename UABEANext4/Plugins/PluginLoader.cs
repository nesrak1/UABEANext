using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Util;

namespace UABEANext4.Plugins;
public class PluginLoader
{
    private readonly List<IUavPluginOption> _pluginOptions = [];
    private readonly List<IUavPluginPreviewer> _pluginPreviewers = [];
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);

    public bool LoadPlugin(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            if (_loadedPaths.Contains(fullPath))
                return true;

            if (!File.Exists(fullPath))
                return false;

            var plugLoadCtx = new PluginLoadContext(fullPath);
            var asm = plugLoadCtx.LoadAssemblyByPath(fullPath);

            bool anyAdded = false;

            foreach (Type type in asm.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (typeof(IUavPluginOption).IsAssignableFrom(type))
                {
                    if (_pluginOptions.Any(o => o.GetType() == type))
                    {
                        continue;
                    }

                    if (Activator.CreateInstance(type) is IUavPluginOption plugInst)
                    {
                        _pluginOptions.Add(plugInst);
                        anyAdded = true;
                    }
                }
                else if (typeof(IUavPluginPreviewer).IsAssignableFrom(type))
                {
                    if (_pluginPreviewers.Any(p => p.GetType() == type))
                    {
                        continue;
                    }

                    if (Activator.CreateInstance(type) is IUavPluginPreviewer plugInst)
                    {
                        _pluginPreviewers.Add(plugInst);
                        anyAdded = true;
                    }
                }
            }

            if (anyAdded)
            {
                _loadedPaths.Add(fullPath);
            }

            return anyAdded;
        }
        catch
        {
            return false;
        }

    }

    public void LoadPluginsInDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.dll"))
        {
            LoadPlugin(file);
        }
    }

    public List<PluginOptionModePair> GetOptionsThatSupport(Workspace workspace, List<AssetInst> assets, UavPluginMode mode)
    {
        var options = new List<PluginOptionModePair>();
        foreach (var option in _pluginOptions)
        {
            var bothOpt = mode & option.Options;
            foreach (var flag in bothOpt.GetUniqueFlags())
            {
                if (flag == UavPluginMode.All)
                    continue;

                var supported = option.SupportsSelection(workspace, flag, assets);
                if (supported)
                    options.Add(new PluginOptionModePair(option, flag));
            }
        }

        return options;
    }

    public List<PluginPreviewerTypePair> GetPreviewersThatSupport(Workspace workspace, AssetInst asset)
    {
        var previewers = new List<PluginPreviewerTypePair>();
        foreach (var previewer in _pluginPreviewers)
        {
            var previewType = previewer.SupportsPreview(workspace, asset);
            if (previewType != UavPluginPreviewerType.None)
                previewers.Add(new PluginPreviewerTypePair(previewer, previewType));
        }

        return previewers;
    }
}
