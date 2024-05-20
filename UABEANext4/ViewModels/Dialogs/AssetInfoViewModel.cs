using System.Collections.Generic;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Logic.AssetInfo;

namespace UABEANext4.ViewModels.Dialogs;

public partial class AssetInfoViewModel : ViewModelBase, IDialogAware
{
    private readonly AssetInfoService _assetInfoService;
    
    public IEnumerable<WorkspaceItem> Items { get; }
    private WorkspaceItem? _selectedItem;
    
    public WorkspaceItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;

            if (_selectedItem is not { Object: AssetsFileInstance inst })
            {
                GeneralInfo = GeneralInfo.Empty;
                return;
            }

            GeneralInfo = _assetInfoService.GetGeneralInfo(inst);
        }
    }
    
    [ObservableProperty] private GeneralInfo? _generalInfo;

    public AssetInfoViewModel()
    {
        Items = [];
        _assetInfoService = new();
    }
    
    public AssetInfoViewModel(IEnumerable<WorkspaceItem> items)
    {
        Items = items;
        _assetInfoService = new();
    }

    public string Title => "Asset Info";
    public int Width => 500;
    public int Height => 450;
}
