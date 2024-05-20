using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Logic.AssetInfo;

namespace UABEANext4.ViewModels.Dialogs;

public partial class AssetInfoViewModel : ViewModelBase, IDialogAware
{
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

            GeneralInfo = new GeneralInfo(inst);
        }
    }

    [ObservableProperty]
    private GeneralInfo? _generalInfo;

    public string Title => "Asset Info";
    public int Width => 500;
    public int Height => 450;

    public AssetInfoViewModel()
    {
        Items = [];
    }

    public AssetInfoViewModel(IEnumerable<WorkspaceItem> items)
    {
        Items = items;
    }
}
