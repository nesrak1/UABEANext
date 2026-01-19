using System.Collections.Generic;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.ViewModels.Documents;

namespace UABEANext4.Plugins;

public class PluginItemInfo
{
    public string Name { get; }

    private IUavPluginOption? _option;
    private AssetDocumentViewModel _docViewModel;

    public PluginItemInfo(string name, IUavPluginOption? option, AssetDocumentViewModel docViewModel)
    {
        Name = name;
        _option = option;
        _docViewModel = docViewModel;
    }

    public async Task Execute(object selectedItems)
    {
        if (_option != null)
        {
            var workspace = _docViewModel.Workspace;
            var res = await _option.Execute(workspace, new UavPluginFunctions(), _option.Options, (List<AssetInst>)selectedItems);
            if (res)
            {
                _docViewModel.ResendSelectedAssetsSelected();
            }
        }
    }

    public override string ToString()
    {
        return Name;
    }
}
