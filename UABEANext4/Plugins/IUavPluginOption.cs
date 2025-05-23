using System.Collections.Generic;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Plugins;
public interface IUavPluginOption
{
    string Name { get; }
    string Description { get; }
    UavPluginMode Options { get; }

    bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection);
    Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection);
}