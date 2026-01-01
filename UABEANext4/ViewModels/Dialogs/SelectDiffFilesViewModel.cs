using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;

namespace UABEANext4.ViewModels.Dialogs;

public partial class SelectDiffFilesViewModel : ViewModelBase, IDialogAware<Tuple<AssetsFileInstance, AssetsFileInstance>?>
{
    public string Title => "Select Files to Compare";
    public int Width => 400;
    public int Height => 200;

    public event Action<Tuple<AssetsFileInstance, AssetsFileInstance>?>? RequestClose;

    public List<AssetsFileInstance> AvailableFiles { get; }

    [ObservableProperty] private AssetsFileInstance? _selectedLeft;
    [ObservableProperty] private AssetsFileInstance? _selectedRight;

    public SelectDiffFilesViewModel(Workspace workspace)
    {
        // Собираем все AssetsFiles, включая те, что внутри бандлов
        AvailableFiles = new List<AssetsFileInstance>();
        
        foreach (var item in workspace.RootItems)
        {
            AddFilesRecursive(item);
        }
    }

    private void AddFilesRecursive(WorkspaceItem item)
    {
        if (item.ObjectType == WorkspaceItemType.AssetsFile && item.Object is AssetsFileInstance afi)
        {
            AvailableFiles.Add(afi);
        }
        
        if (item.Children != null)
        {
            foreach (var child in item.Children)
            {
                AddFilesRecursive(child);
            }
        }
    }

    public void BtnCompare_Click()
    {
        if (SelectedLeft != null && SelectedRight != null)
        {
            RequestClose?.Invoke(new Tuple<AssetsFileInstance, AssetsFileInstance>(SelectedLeft, SelectedRight));
        }
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}
