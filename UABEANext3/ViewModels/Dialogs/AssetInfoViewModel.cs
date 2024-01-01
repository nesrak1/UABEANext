using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetsTools.NET.Extra;
using Avalonia;
using ReactiveUI.Fody.Helpers;
using UABEANext3.AssetWorkspace;
using UABEANext3.Models;
using UABEANext3.Models.AssetInfo;

namespace UABEANext3.ViewModels.Dialogs;

public class AssetInfoViewModel : ViewModelBase
{
    private readonly WorkspaceItem? _rootItem;
    private readonly AssetInfoService _assetInfoService;
    private WorkspaceItem? _selectedItem;
    
    public List<WorkspaceItem> Items { get; }

    public WorkspaceItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;

            if (_selectedItem is not { Object: AssetsFileInstance inst })
                return;

            GeneralInfo = _assetInfoService.GetGeneralInfo(inst);
        }
    }

    [Reactive]
    public GeneralInfo? GeneralInfo { get; set; }

    public AssetInfoViewModel()
    {
        Items = new();
        _assetInfoService = new();
    }
    
    public AssetInfoViewModel(WorkspaceItem rootItem)
    {
        _rootItem = rootItem;
        Items = rootItem.Children;
        _assetInfoService = new();
    }
}
