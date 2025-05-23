using System;
using System.Collections.Generic;
using System.IO;
using UABEANext4.AssetWorkspace;
using UABEANext4.Util;

namespace UABEANext4.Plugins;
public class PluginLoader
{
    private readonly List<IUavPluginOption> _pluginOptions = [];
    private readonly List<IUavPluginPreviewer> _pluginPreviewers = [];

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

                    _pluginOptions.Add(plugInst);
                }
                else if (typeof(IUavPluginPreviewer).IsAssignableFrom(type))
                {
                    object? typeInst = Activator.CreateInstance(type);
                    if (typeInst == null)
                        return false;

                    if (typeInst is not IUavPluginPreviewer plugInst)
                        return false;

                    _pluginPreviewers.Add(plugInst);
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
