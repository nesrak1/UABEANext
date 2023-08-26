using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.Logic.Plugins
{
    public class PluginManager
    {
        private List<UABEAPluginInfo> _loadedPlugins = new List<UABEAPluginInfo>();

        public bool LoadPlugin(string path)
        {
            try
            {
                Assembly asm = Assembly.LoadFrom(path);
                foreach (Type type in asm.GetTypes())
                {
                    if (typeof(UABEAPlugin).IsAssignableFrom(type))
                    {
                        object? typeInst = Activator.CreateInstance(type);
                        if (typeInst == null)
                            return false;

                        UABEAPlugin plugInst = (UABEAPlugin)typeInst;
                        UABEAPluginInfo plugInf = plugInst.GetInfo();
                        _loadedPlugins.Add(plugInf);
                        return true;
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
                var success = LoadPlugin(file);
            }
        }

        public List<UABEAPluginMenuInfo> GetPluginsThatSupport(AssetsManager am, List<AssetInst> selectedAssets)
        {
            List<UABEAPluginMenuInfo> menuInfos = new List<UABEAPluginMenuInfo>();
            foreach (var pluginInf in _loadedPlugins)
            {
                foreach (var option in pluginInf.Options)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var action = i == 0 ? UABEAPluginAction.Import : UABEAPluginAction.Export;
                        var supported = option.SelectionValid(am, action, selectedAssets, out string entryName);
                        if (supported)
                        {
                            UABEAPluginMenuInfo menuInf = new UABEAPluginMenuInfo(pluginInf, option, entryName);
                            menuInfos.Add(menuInf);
                        }
                    }
                }
            }
            return menuInfos;
        }
    }
}
