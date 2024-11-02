using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
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
                TypeTreeInfo = TypeTreeInfo.Empty;
                ExternalsInfo = ExternalInfo.Empty;
                ScriptsInfo = ScriptInfo.Empty;
                return;
            }

            GeneralInfo = new GeneralInfo(inst);
            TypeTreeInfo = new TypeTreeInfo(_workspace, inst);
            ExternalsInfo = new ExternalInfo(_workspace, inst);
            ScriptsInfo = new ScriptInfo(_workspace, inst);
        }
    }

    [ObservableProperty]
    private GeneralInfo? _generalInfo;
    [ObservableProperty]
    private TypeTreeInfo? _typeTreeInfo;
    [ObservableProperty]
    private ExternalInfo? _externalsInfo;
    [ObservableProperty]
    private ScriptInfo? _scriptsInfo;

    private readonly Workspace _workspace;

    public string Title => "Asset Info";
    public int Width => 500;
    public int Height => 450;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public AssetInfoViewModel()
    {
        Items = [];
        _workspace = new();
    }

    public AssetInfoViewModel(Workspace workspace, IEnumerable<WorkspaceItem> items)
    {
        Items = items;
        _workspace = workspace;
    }
}
