using System;
using System.Collections.Generic;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using UABEANext4.AssetInfo;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;

namespace UABEANext4.ViewModels.Dialogs;

public partial class AssetInfoViewModel : ViewModelBase, IDialogAware<string>
{
    private readonly AssetInfoService _assetInfoService;
    
    [ObservableProperty] private GeneralInfo? _generalInfo;

    public AssetInfoViewModel()
    {
        _assetInfoService = new();
    }
    
    public AssetInfoViewModel(AssetsFileInstance assetsFileInstance)
    {
        _assetInfoService = new();
        GeneralInfo = _assetInfoService.GetGeneralInfo(assetsFileInstance);
    }

    public string Title => "Asset General Info";
    public int Width => 450;
    public int Height => 300;
    public event Action<string?>? RequestClose;
}
