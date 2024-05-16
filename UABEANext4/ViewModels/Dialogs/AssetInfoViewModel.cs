using System.Collections.Generic;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using UABEANext4.AssetInfo;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.ViewModels.Dialogs;

public partial class AssetInfoViewModel : ViewModelBase
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

    [ObservableProperty] private GeneralInfo? _generalInfo;

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
