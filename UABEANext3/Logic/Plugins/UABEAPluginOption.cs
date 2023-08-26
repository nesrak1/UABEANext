using AssetsTools.NET.Extra;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.Logic.Plugins
{
    public interface UABEAPluginOption
    {
        public bool SelectionValid(AssetsManager am, UABEAPluginAction action, List<AssetInst> selection, out string name);
        public Task<bool> Execute(Window win, Workspace workspace, List<AssetInst> selection);
    }
}
