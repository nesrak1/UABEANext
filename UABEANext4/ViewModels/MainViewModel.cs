using AssetsTools.NET.Extra;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Services;
using UABEANext4.Util;
using UABEANext4.ViewModels.Dialogs;
using UABEANext4.ViewModels.Documents;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public IRootDock? _layout;

    [ObservableProperty]
    public bool _dockWorkspaceExplorerVisible = true;
    [ObservableProperty]
    public bool _dockHierarchyVisible = true;
    [ObservableProperty]
    public bool _dockInspectorVisible = true;
    [ObservableProperty]
    public bool _dockPreviewerVisible = true;
    [ObservableProperty]
    public bool _dockSceneViewVisible = true;
    [ObservableProperty]
    public bool _loadContainers = false;

    public Workspace Workspace { get; }

    public bool UsesChrome => OperatingSystem.IsWindows();

    public ExtendClientAreaChromeHints ChromeHints => UsesChrome
        ? ExtendClientAreaChromeHints.PreferSystemChrome
        : ExtendClientAreaChromeHints.Default;

    private readonly MainDockFactory _factory;
    private List<AssetsFileInstance> _lastLoadedFiles = [];

    public MainViewModel()
    {
        Workspace = new();
        _factory = new MainDockFactory(Workspace);
        Layout = _factory.CreateLayout();
        if (Layout is not null)
        {
            _factory.InitLayout(Layout);
        }

        WeakReferenceMessenger.Default.Register<SelectedWorkspaceItemChangedMessage>(this, (r, h) => _ = OnSelectedWorkspaceItemsChanged(r, h));
        WeakReferenceMessenger.Default.Register<RequestEditAssetMessage>(this, OnRequestEditAsset);
        WeakReferenceMessenger.Default.Register<RequestVisitAssetMessage>(this, (r, h) => _ = OnRequestVisitAsset(r, h));

        _factory.DockableAdded += FactoryDockableAdded;
        _factory.DockableClosed += FactoryDockableClosed;
        _factory.FocusedDockableChanged += FactoryDockableFocused;
    }

    // todo: split out

    #region Dockable toggles
    private void FactoryDockableAdded(object? sender, DockableAddedEventArgs e)
    {
        if (e.Dockable is not IDockable dockable)
            return;

        switch (dockable.Id)
        {
            case "WorkspaceExplorer": DockWorkspaceExplorerVisible = true; break;
            case "Hierarchy": DockHierarchyVisible = true; break;
            case "Inspector": DockInspectorVisible = true; break;
            case "Previewer": DockPreviewerVisible = true; break;
            case "SceneView": DockSceneViewVisible = true; break;
        }
    }

    private void FactoryDockableClosed(object? sender, DockableClosedEventArgs e)
    {
        if (e.Dockable is not IDockable dockable)
            return;

        switch (dockable.Id)
        {
            case "WorkspaceExplorer": DockWorkspaceExplorerVisible = false; break;
            case "Hierarchy": DockHierarchyVisible = false; break;
            case "Inspector": DockInspectorVisible = false; break;
            case "Previewer": DockPreviewerVisible = false; break;
            case "SceneView": DockSceneViewVisible = false; break;
        }
    }

    private void FactoryDockableFocused(object? sender, FocusedDockableChangedEventArgs e)
    {
        if (e.Dockable is Document document)
            _factory.DocMan.LastFocusedDocument = document;
    }

    partial void OnDockWorkspaceExplorerVisibleChanged(bool value)
    {
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");
        if (explorer is null || Layout is null)
            return;

        ShowHideDockable(explorer, value);
    }

    partial void OnDockHierarchyVisibleChanged(bool value)
    {
        var hierarchy = _factory.GetDockable<HierarchyToolViewModel>("Hierarchy");
        if (hierarchy is null || Layout is null)
            return;

        ShowHideDockable(hierarchy, value);
    }

    partial void OnDockInspectorVisibleChanged(bool value)
    {
        var inspector = _factory.GetDockable<InspectorToolViewModel>("Inspector");
        if (inspector is null || Layout is null)
            return;

        ShowHideDockable(inspector, value);
    }

    partial void OnDockPreviewerVisibleChanged(bool value)
    {
        var previewer = _factory.GetDockable<PreviewerToolViewModel>("Previewer");
        if (previewer is null || Layout is null)
            return;

        ShowHideDockable(previewer, value);
    }

    partial void OnDockSceneViewVisibleChanged(bool value)
    {
        var sceneView = _factory.GetDockable<SceneViewToolViewModel>("SceneView");
        if (sceneView is null || Layout is null)
            return;

        ShowHideDockable(sceneView, value);
    }

    private void ShowHideDockable(IDockable dockable, bool show)
    {
        if (show)
        {
            if (dockable.Owner is IDock dock && HasPathToRoot(dock))
            {
                _factory.AddDockable(dock, dockable);
                _factory.SetActiveDockable(dockable);
                _factory.SetFocusedDockable(dock, dockable);
            }
            else if (_factory.MainPane is not null)
            {
                _factory.AddDockable(_factory.MainPane, dockable);
                _factory.FloatDockable(dockable); // make floating for now, no idea where else to put it
                _factory.SetActiveDockable(dockable);
                _factory.SetFocusedDockable(_factory.MainPane, dockable);
            }
        }
        else
        {
            _factory.CloseDockable(dockable);
        }
    }

    // a very roundabout way to check if a dock is visible all the way to the root
    private bool HasPathToRoot(IDockable baseDockable)
    {
        IDockable? dockable = baseDockable;
        while (true)
        {
            if (dockable is null || dockable.Owner is null)
                return false;

            if (dockable.Owner is not IDock parentDock)
                return false;

            if (parentDock.VisibleDockables is null || !parentDock.VisibleDockables.Contains(dockable))
                return false;

            dockable = dockable.Owner;
            if (dockable == Layout)
                return true;
        }
    }
    #endregion

    #region Menu items
    public async Task OpenFiles(IEnumerable<string?> enumerable)
    {
        int totalCount = enumerable.Count();
        if (totalCount == 0)
        {
            return;
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount - 1, 4)
        };

        Workspace.SetProgressThreadSafe(0f, "Loading files...");
        await Task.Run(() =>
        {
            Workspace.ModifyMutex.WaitOne();
            Workspace.ProgressValue = 0;
            int startLoadOrder = Workspace.NextLoadIndex;
            int currentCount = 0;
            Parallel.ForEach(enumerable, options, (fileName, state, index) =>
            {
                if (fileName is not null)
                {
                    try
                    {
                        var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        var file = Workspace.LoadAnyFile(fileStream, startLoadOrder + (int)index);
                        var currentCountNow = Interlocked.Increment(ref currentCount);
                        var currentProgress = currentCountNow / (float)totalCount;
                        Workspace.SetProgressThreadSafe(currentProgress, "Loaded " + Path.GetFileName(fileName));
                    }
                    catch
                    {
                        var currentCountNow = Interlocked.Increment(ref currentCount);
                        var currentProgress = currentCountNow / (float)totalCount;
                        Workspace.SetProgressThreadSafe(currentProgress, "Skipping " + Path.GetFileName(fileName));
                    }
                }
            });
            Workspace.SetProgressThreadSafe(1f, "Done");
            Workspace.ModifyMutex.ReleaseMutex();
        });

        if (Workspace.Manager.ClassDatabase == null)
        {
            var anySerializedItems = false;
            foreach (var rootItem in Workspace.RootItems)
            {
                if (rootItem.ObjectType == WorkspaceItemType.AssetsFile)
                {
                    anySerializedItems = true;
                    break;
                }
                else if (rootItem.ObjectType == WorkspaceItemType.BundleFile)
                {
                    foreach (var childItem in rootItem.Children)
                    {
                        if (rootItem.ObjectType == WorkspaceItemType.AssetsFile)
                        {
                            anySerializedItems = true;
                            break;
                        }
                    }
                }

                if (anySerializedItems)
                {
                    break;
                }
            }

            if (anySerializedItems)
            {
                var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
                var version = await dialogService.ShowDialog(new VersionSelectViewModel());
                if (version != null)
                {
                    Workspace.Manager.LoadClassDatabaseFromPackage(version);
                }
            }
        }
    }

    public async void FileOpen()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a file",
            FileTypeFilter = new FilePickerFileType[]
            {
                new FilePickerFileType("All files (*.*)") { Patterns = new[] { "*" } }
            },
            AllowMultiple = true
        });

        var fileNames = FileDialogUtils.GetOpenFileDialogFiles(result);
        await OpenFiles(fileNames);
    }

    private async Task DoSaveOverwrite(IEnumerable<WorkspaceItem> items)
    {
        Workspace.ModifyMutex.WaitOne();
        try
        {
            var rootItems = new HashSet<WorkspaceItem>();
            foreach (var item in items)
            {
                var rootItem = item;
                while (rootItem.Parent != null)
                {
                    rootItem = rootItem.Parent;
                }
                rootItems.Add(rootItem);
            }

            var fileInstsToReload = new HashSet<AssetsFileInstance>();
            var someFailed = false;
            foreach (var item in rootItems)
            {
                var (saved, failed) = await Workspace.Save(item);
                if (failed)
                {
                    someFailed = true;
                    continue;
                }
                else if (!saved)
                {
                    continue;
                }

                if (item.Object is AssetsFileInstance fileInst)
                {
                    fileInstsToReload.Add(fileInst);
                }
                else if (item.Object is BundleFileInstance)
                {
                    foreach (var child in item.Children)
                    {
                        if (child.Object is AssetsFileInstance childInst)
                        {
                            fileInstsToReload.Add(childInst);
                        }
                    }
                }
            }

            if (fileInstsToReload.Count == 0)
            {
                if (someFailed)
                    Workspace.SetProgressThreadSafe(1f, "All files failed to save (check if you have write access?)");
                else
                    Workspace.SetProgressThreadSafe(1f, "No files open to save");
            }
            else
            {
                await ReloadAssetDocuments(fileInstsToReload);
                if (someFailed)
                    Workspace.SetProgressThreadSafe(1f, "Saved (some failed), with open saved files reloaded");
                else
                    Workspace.SetProgressThreadSafe(1f, "Saved, with open saved files reloaded");
            }
        }
        finally
        {
            Workspace.ModifyMutex.ReleaseMutex();
        }
    }

    private async Task DoSaveCopy(IEnumerable<WorkspaceItem> items)
    {
        Workspace.ModifyMutex.WaitOne();
        try
        {
            var rootItems = new HashSet<WorkspaceItem>();
            foreach (var item in items)
            {
                var rootItem = item;
                while (rootItem.Parent != null)
                {
                    rootItem = rootItem.Parent;
                }
                rootItems.Add(rootItem);
            }

            foreach (var item in rootItems)
            {
                await Workspace.SaveAs(item);
            }

            Workspace.SetProgressThreadSafe(1f, "Saved");
        }
        finally
        {
            Workspace.ModifyMutex.ReleaseMutex();
        }
    }

    public async Task FileSave()
    {
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");
        if (explorer == null)
            return;

        var items = explorer.SelectedItems.Cast<WorkspaceItem>();
        await DoSaveOverwrite(items);
    }

    // more like "save copy as"
    public async Task FileSaveAs()
    {
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");
        if (explorer == null)
            return;

        var items = explorer.SelectedItems.Cast<WorkspaceItem>();
        await DoSaveCopy(items);
    }

    public async Task FileSaveAll()
    {
        await DoSaveOverwrite(Workspace.RootItems);
    }

    public async Task FileSaveAllAs()
    {
        await DoSaveCopy(Workspace.RootItems);
    }

    public void FileCloseAll()
    {
        Workspace.CloseAll();
        WeakReferenceMessenger.Default.Send(new WorkspaceClosingMessage());

        var files = _factory.GetDockable<IDocumentDock>("Files");
        if (files is not null && files.VisibleDockables != null && files.VisibleDockables.Count > 0)
        {
            // lol you have to pass in a child
            _factory.CloseAllDockables(files.VisibleDockables[0]);

            _factory.DocMan.Clear();
        }
    }

    public void ViewDuplicateTab()
    {
        var files = _factory.GetDockable<IDocumentDock>("Files");
        if (Layout is not null && files is not null)
        {
            if (files.ActiveDockable != null)
            {
                var oldDockable = files.ActiveDockable;
                _factory.AddDockable(files, oldDockable);
            }
        }
    }

    // todo: should we just replace every assetinst? is that too expensive?
    // would it be better than unselecting everything?
    private async Task ReloadAssetDocuments(HashSet<AssetsFileInstance> fileInst)
    {
        foreach (var dockable in _factory.DocMan.Documents)
        {
            if (dockable is not AssetDocumentViewModel document)
                continue;

            var matchesAny = document.FileInsts.Intersect(fileInst).Any();
            if (matchesAny)
            {
                await document.Load(document.FileInsts);
            }
        }
    }
    #endregion

    private async Task OnSelectedWorkspaceItemsChanged(object recipient, SelectedWorkspaceItemChangedMessage message)
    {
        await OpenAssetDocument(message.Value, true);
    }

    private async Task<AssetDocumentViewModel?> OpenAssetDocument(List<WorkspaceItem> workspaceItems, bool replaceDock)
    {
        AssetDocumentViewModel document;
        if (workspaceItems.Count == 1)
        {
            var workspaceItem = workspaceItems[0];

            if (workspaceItem.ObjectType != WorkspaceItemType.AssetsFile)
                return null;

            if (workspaceItem.Object is not AssetsFileInstance mainFileInst)
                return null;

            document = new AssetDocumentViewModel(Workspace, LoadContainers)
            {
                Title = mainFileInst.name,
                Id = mainFileInst.name
            };

            _lastLoadedFiles = [mainFileInst];
            await document.Load(_lastLoadedFiles);
        }
        else
        {
            var assetsFileItems = workspaceItems
                .Where(i => i.ObjectType == WorkspaceItemType.AssetsFile)
                .Select(i => (AssetsFileInstance?)i.Object)
                .Where(i => i != null)
                .ToList();

            if (assetsFileItems.Count == 0)
                return null;

            if (assetsFileItems[0] is not AssetsFileInstance mainFileInst)
                return null;

            document = new AssetDocumentViewModel(Workspace, LoadContainers)
            {
                Title = $"{mainFileInst.name} and {assetsFileItems.Count - 1} other files"
            };

            _lastLoadedFiles = assetsFileItems!;
            await document.Load(_lastLoadedFiles);
        }

        var files = _factory.GetDockable<IDocumentDock>("Files");
        if (Layout is not null && files is not null)
        {
            if (files.ActiveDockable != null && replaceDock)
            {
                var oldDockable = files.ActiveDockable;
                _factory.AddDockable(files, document);
                _factory.SwapDockable(files, oldDockable, document);
                _factory.CloseDockable(oldDockable);
                _factory.SetActiveDockable(document);
                _factory.SetFocusedDockable(files, document);

                if (oldDockable is Document oldDockableDocument)
                    _factory.DocMan.Documents.Remove(oldDockableDocument);
            }
            else
            {
                _factory.AddDockable(files, document);
                _factory.SetActiveDockable(document);
                _factory.SetFocusedDockable(files, document);
            }

            _factory.DocMan.Documents.Add(document);
            _factory.DocMan.LastFocusedDocument = document;
        }

        return document;
    }

    private void OnRequestEditAsset(object recipient, RequestEditAssetMessage message)
    {
        _ = ShowEditAssetDialog(message.Value);
    }

    private async Task OnRequestVisitAsset(object recipient, RequestVisitAssetMessage message)
    {
        var asset = message.Value;
        var lastFocusedDoc = _factory.DocMan.LastFocusedDocument;
        AssetDocumentViewModel? foundAssetDocVm = null;

        // best case scenario: last selected document contains this asset
        if (lastFocusedDoc is AssetDocumentViewModel assetDocVm
            && assetDocVm.Items.Contains(asset))
        {
            foundAssetDocVm = assetDocVm;
            goto finish;
        }

        // second best case scenario: the last selected document is
        // a blank document we can open the containing file in.
        var wsItem = Workspace.FindWorkspaceItemByInstance(asset.FileInstance);
        if (wsItem is not null)
        {
            var replaceDock = lastFocusedDoc is BlankDocumentViewModel;
            var newAssetDocVm = await OpenAssetDocument([wsItem], replaceDock);
            if (newAssetDocVm is not null)
            {
                foundAssetDocVm = newAssetDocVm;
                goto finish;
            }
        }

        // neither of those were the case. hopefully one of the open
        // asset documents contains this asset?
        foreach (var dock in _factory.DocMan.Documents)
        {
            // we already checked this one, skip
            if (dock == lastFocusedDoc)
                continue;

            if (dock is not AssetDocumentViewModel otherAssetDocVm)
                continue;

            if (otherAssetDocVm.Items.Contains(asset))
            {
                foundAssetDocVm = otherAssetDocVm;
                goto finish;
            }
        }

        // give up
        await MessageBoxUtil.ShowDialog("Error", "Couldn't find asset document to show this asset in.");
        return;

    finish:
        if (foundAssetDocVm is not null)
        {
            foundAssetDocVm.SetSelectedItems([asset]);

            var files = _factory.GetDockable<IDocumentDock>("Files");
            if (Layout is not null && files is not null)
            {
                // if the document isn't in the Files dock, it won't be focused
                // I think this is fine for now, but it'd be good to find a way
                // to bring forward a window if it's popped out.
                _factory.SetFocusedDockable(files, foundAssetDocVm);
            }
        }
    }

    private async Task ShowEditAssetDialog(AssetInst asset)
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var baseField = Workspace.GetBaseField(asset);
        if (baseField == null)
        {
            return;
        }

        var refMan = Workspace.Manager.GetRefTypeManager(asset.FileInstance);
        Workspace.CheckAndSetMonoTempGenerators(asset.FileInstance, asset);
        var newData = await dialogService.ShowDialog(new EditDataViewModel(baseField, refMan));
        if (newData != null)
        {
            asset.UpdateAssetDataAndRow(Workspace, newData);
            WeakReferenceMessenger.Default.Send(new AssetsUpdatedMessage(asset));
        }
    }

    public async Task ShowAssetInfoDialog()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");

        if (explorer is null)
        {
            return;
        }

        HashSet<WorkspaceItem> items = [];
        if (explorer.SelectedItems.Count != 0)
        {
            foreach (var item in WorkspaceItem.GetAssetsFileWorkspaceItems(explorer.SelectedItems.OfType<WorkspaceItem>()))
            {
                items.Add(item);
            }
        }
        else
        {
            foreach (var item in WorkspaceItem.GetAssetsFileWorkspaceItems(Workspace.RootItems))
            {
                items.Add(item);
            }
        }

        await dialogService.ShowDialog(new AssetInfoViewModel(Workspace, items));
    }

    public async Task ShowSearchBytesDialog()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");

        if (explorer is null)
        {
            return;
        }

        List<AssetsFileInstance> fileInsts;
        var lastFocusedDoc = _factory.DocMan.LastFocusedDocument;
        if (lastFocusedDoc is AssetDocumentViewModel assetDocVm)
        {
            fileInsts = assetDocVm.FileInsts;
        }
        else
        {
            // fallback to all items
            fileInsts = [];
            foreach (var item in WorkspaceItem.GetAssetsFileWorkspaceItems(Workspace.RootItems))
            {
                if (item.Object is AssetsFileInstance fileInst)
                {
                    fileInsts.Add(fileInst);
                }
            }
        }

        await dialogService.ShowDialog(new AssetDataSearchViewModel(Workspace, fileInsts));
    }
}
