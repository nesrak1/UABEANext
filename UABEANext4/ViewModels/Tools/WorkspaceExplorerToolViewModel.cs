using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Services;
using UABEANext4.Util;
using UABEANext4.ViewModels.Dialogs;
using UABEANext4.ViewModels.Menu;

namespace UABEANext4.ViewModels.Tools;

public partial class WorkspaceExplorerToolViewModel : Tool
{
    const string TOOL_TITLE = "Workspace Explorer";

    public delegate void SelectedWorkspaceItemChangedEvent(List<WorkspaceItem> workspaceItems);

    public Workspace Workspace { get; }

    [ObservableProperty]
    public AvaloniaList<object> _selectedItems;
    [ObservableProperty]
    public bool _selectOpensFiles = true;

    [ObservableProperty]
    private ObservableCollection<MenuOptionViewModel> _contextMenuItems = [];

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public WorkspaceExplorerToolViewModel()
    {
        Workspace = new();
        SelectedItems = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }

    public WorkspaceExplorerToolViewModel(Workspace workspace)
    {
        Workspace = workspace;
        SelectedItems = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;
    }

    public void SelectedItemsChanged(List<WorkspaceItem> value)
    {
        if (SelectOpensFiles)
        {
            WeakReferenceMessenger.Default.Send(new SelectedWorkspaceItemChangedMessage(value));
        }
    }

    public async void RenameItem()
    {
        var wsItem = (WorkspaceItem)SelectedItems[0];
        if (!IsItemRenamable(wsItem))
        {
            return;
        }

        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var newName = await dialogService.ShowDialog(new RenameFileViewModel(wsItem.Name));
        if (newName == null)
        {
            return;
        }

        Workspace.RenameFile(wsItem, newName);
    }

    public async void EditBundleFiles()
    {
        await MessageBoxUtil.ShowDialog("Not implemented", "Not implemented yet, come back later!");
    }

    public async void AddRemoveFilesFromBundle()
    {
        await MessageBoxUtil.ShowDialog("Not implemented", "Not implemented yet, come back later!");
    }

    public async void ImportFilesIntoBundle()
    {
        await MessageBoxUtil.ShowDialog("Not implemented", "Not implemented yet, come back later!");
    }

    public async void ExportFilesFromBundle()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            Title = "Select export directory",
            AllowMultiple = false
        });

        var folders = FileDialogUtils.GetOpenFolderDialogFolders(result);
        if (folders.Length == 0)
        {
            return;
        }

        var folder = folders[0];

        var selectedItems = GetSelectedWsItems();
        foreach (var item in selectedItems)
        {
            var destPath = Path.Combine(folder, item.Name);

            // skip workspace items not in bundles
            if (item.Parent is null)
                continue;

            var parentItem = item.Parent;

            // this shouldn't happen
            if (parentItem.Object is not BundleFileInstance bunInst)
                continue;

            if (item.Object is AssetsFileInstance fileInst)
            {
                // we are looking at a file instance (which could have modified assets inside)
                if (Workspace.ModifiedItems.Contains(item))
                {
                    // file was modified, so use Write to save out modified version
                    using var writer = new AssetsFileWriter(destPath);
                    fileInst.file.Write(writer);
                    continue;
                }
            }

            // either we now have an assets file that's not modified, or we have a
            // a resource file (resS, etc.) that is or isn't modified. since resources
            // files don't have replacers, it's fine to use the same code path.
            // we need to use OriginalName because the directory info won't update until
            // another save. OriginalName will update when we save, so this is safe.
            var fileDirInfo = BundleHelper.GetDirInfo(bunInst.file, item.OriginalName);
            if (fileDirInfo.Replacer is not null)
            {
                using var fileStream = File.OpenWrite(destPath);
                var previewStream = fileDirInfo.Replacer.GetPreviewStream();
                lock (previewStream)
                {
                    previewStream.Position = 0;
                    previewStream.CopyTo(fileStream);
                }
            }
            else
            {
                using var fileStream = File.OpenWrite(destPath);
                var bundleReader = bunInst.file.DataReader;
                lock (bundleReader)
                {
                    bundleReader.Position = fileDirInfo.Offset;
                    Net35Polyfill.CopyToCompat(bundleReader.BaseStream, fileStream, fileDirInfo.DecompressedSize);
                }
            }
        }
    }

    public async void UnloadItems()
    {
        var itemsToUnload = new List<object>(SelectedItems);
        foreach (var item in itemsToUnload)
        {
            if (item is not WorkspaceItem wsItem || wsItem.Parent is not null)
                continue;

            WeakReferenceMessenger.Default.Send(new RequestCloseFileMessage(wsItem));
        }
    }

    public void LoadAll()
    {
        var itemsToLoad = new List<WorkspaceItem>();
        foreach (var rootItem in Workspace.RootItems)
            GatherWorkspaceItemsRecursive(rootItem, itemsToLoad);

        SelectedItemsChanged(itemsToLoad);
    }

    public void CreateContextMenu()
    {
        ContextMenuItems.Clear();
        if (SelectedItems.Count == 0)
            return;

        // nothing selected or nothing applies
        bool showDefaultCm = false;
        // single bundle selected
        bool showAddRemoveCm = false;
        // single file with bundle parent selected
        bool showRenameCm = false;
        // any root (parentless) objects selected
        bool showUnloadCm = false;
        // files with bundle parent selected
        bool showImportExportCm = false;

        var selectedItems = GetSelectedWsItems();
        if (selectedItems.Count == 0)
        {
            showDefaultCm = true;
        }
        else if (selectedItems.Count == 1)
        {
            var selectedItem = selectedItems[0];
            if (selectedItem.ObjectType == WorkspaceItemType.BundleFile)
            {
                showAddRemoveCm = true;
                showUnloadCm = true;
            }
            else if (
                selectedItem.ObjectType == WorkspaceItemType.AssetsFile ||
                selectedItem.ObjectType == WorkspaceItemType.ResourceFile)
            {
                if (selectedItem.Parent is not null)
                {
                    showRenameCm = true;
                    showImportExportCm = true;
                }
                else
                {
                    showUnloadCm = true;
                }
            }
            else
            {
                showDefaultCm = true;
            }
        }
        else
        {
            bool anyUnloadable = selectedItems.Any(i =>
                i.ObjectType == WorkspaceItemType.BundleFile ||
                (i.Parent is null &&
                    (i.ObjectType == WorkspaceItemType.AssetsFile ||
                    i.ObjectType == WorkspaceItemType.ResourceFile)));

            if (anyUnloadable)
            {
                showUnloadCm = true;
            }

            bool anyEditable = selectedItems.Any(i => i.Parent is not null &&
                (i.ObjectType == WorkspaceItemType.AssetsFile ||
                i.ObjectType == WorkspaceItemType.ResourceFile));

            if (anyEditable)
            {
                showImportExportCm = true;
            }

            if (!anyUnloadable && !anyEditable)
            {
                showDefaultCm = true;
            }
        }

        if (showDefaultCm)
        {
            ContextMenuItems.Add(new MenuOptionViewModel(
                "No options available",
                new RelayCommand(() => { }),
                null,
                ApplicationExtensions.GetIconPath("action-view-info.png")
            ));
        }
        if (showAddRemoveCm)
        {
            ContextMenuItems.Add(new MenuOptionViewModel(
                "Add/remove files",
                new RelayCommand(AddRemoveFilesFromBundle),
                null,
                ApplicationExtensions.GetIconPath("action-add-asset.png")
            ));
        }
        if (showRenameCm)
        {
            ContextMenuItems.Add(new MenuOptionViewModel(
                "Rename",
                new RelayCommand(RenameItem),
                null,
                ApplicationExtensions.GetIconPath("action-edit.png")
            ));
        }
        if (showUnloadCm)
        {
            ContextMenuItems.Add(new MenuOptionViewModel(
                "Unload",
                new RelayCommand(UnloadItems),
                null,
                ApplicationExtensions.GetIconPath("action-unload.png")
            ));
        }
        if (showImportExportCm)
        {
            ContextMenuItems.Add(new MenuOptionViewModel(
                "Import",
                new RelayCommand(ImportFilesIntoBundle),
                null,
                ApplicationExtensions.GetIconPath("action-import-asset.png")
            ));
            ContextMenuItems.Add(new MenuOptionViewModel(
                "Export",
                new RelayCommand(ExportFilesFromBundle),
                null,
                ApplicationExtensions.GetIconPath("action-export-asset.png")
            ));
        }
    }

    partial void OnSelectOpensFilesChanged(bool value)
    {
        // fire selected items event if auto open is turned on
        if (value)
        {
            var selectedItems = GetSelectedWsItems();
            if (selectedItems.Count > 0)
            {
                SelectedItemsChanged(selectedItems);
            }
        }
    }

    private bool IsItemRenamable(WorkspaceItem wsItem)
    {
        return wsItem.Parent != null && wsItem.Parent.ObjectType == WorkspaceItemType.BundleFile;
    }

    private void GatherWorkspaceItemsRecursive(WorkspaceItem thisItem, List<WorkspaceItem> allItems)
    {
        if (thisItem.ObjectType == WorkspaceItemType.AssetsFile)
            allItems.Add(thisItem);

        foreach (var childItem in thisItem.Children)
        {
            GatherWorkspaceItemsRecursive(childItem, allItems);
        }
    }

    private List<WorkspaceItem> GetSelectedWsItems()
    {
        var wsItems = new List<WorkspaceItem>();
        foreach (var item in SelectedItems)
        {
            if (item is WorkspaceItem wsItem)
            {
                wsItems.Add(wsItem);
            }
        }

        return wsItems;
    }
}
