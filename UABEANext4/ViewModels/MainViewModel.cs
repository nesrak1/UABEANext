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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Configuration;
using UABEANext4.Services;
using UABEANext4.Util;
using UABEANext4.ViewModels.Dialogs;
using UABEANext4.ViewModels.Documents;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // dock properties
    [ObservableProperty]
    public IRootDock? _layout;

    private readonly MainDockFactory _factory;

    [ObservableProperty]
    public bool _dockWorkspaceExplorerVisible = true;
    [ObservableProperty]
    public bool _dockHierarchyVisible = true;
    [ObservableProperty]
    public bool _dockInspectorVisible = true;
    [ObservableProperty]
    public bool _dockPreviewerVisible = true;

    public Workspace Workspace { get; }

    // window properties
    private const string DefaultWindowTitle = "UABEANext";

    [ObservableProperty]
    private string _windowTitle = DefaultWindowTitle;

    public bool UsesChrome => OperatingSystem.IsWindows();
    public ExtendClientAreaChromeHints ChromeHints => UsesChrome
        ? ExtendClientAreaChromeHints.PreferSystemChrome
        : ExtendClientAreaChromeHints.Default;

    // state
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
        WeakReferenceMessenger.Default.Register<RequestEditAssetMessage>(this, (r, h) => ShowEditAssetDialog(h.Value));
        WeakReferenceMessenger.Default.Register<RequestCloseFileMessage>(this, OnRequestCloseFile);
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
        }

        if (e.Dockable is Document document && _factory.DocMan.LastFocusedDocument == document)
            _factory.DocMan.LastFocusedDocument = null;
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
    public async Task OpenFiles(IEnumerable<string?> paths)
    {
        var databaseStartedUnloaded = Workspace.Manager.ClassDatabase == null;

        var filePaths = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
                filePaths.Add(path);
            if (Directory.Exists(path))
                filePaths.AddRange(Directory.GetFiles(path, "*", SearchOption.AllDirectories));
        }

        var totalCount = filePaths.Count;
        if (totalCount == 0)
        {
            return;
        }

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount - 1, 4)
        };

        Workspace.SetProgressThreadSafe(0f, "Loading files...");

        var duplicateFilesList = new List<DuplicateLoadInfo>();
        var stackTraceSb = new StringBuilder();

        await Task.Run(() =>
        {
            Workspace.ModifyMutex.WaitOne();
            Workspace.ProgressValue = 0;

            var startLoadOrder = Workspace.NextLoadIndex;
            var currentCount = 0;
            var anyLoaded = false;

            Parallel.ForEach(filePaths, options, (fileName, state, index) =>
            {
                if (fileName is not null)
                {
                    try
                    {
                        var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        Workspace.LoadAnyFile(fileStream, startLoadOrder + (int)index);

                        var currentCountNow = Interlocked.Increment(ref currentCount);
                        var currentProgress = currentCountNow / (float)totalCount;
                        anyLoaded = true;
                        Workspace.SetProgressThreadSafe(currentProgress, "Loaded " + Path.GetFileName(fileName));
                    }
                    catch (DuplicateWorkspaceFileException dupEx)
                    {
                        lock (duplicateFilesList)
                        {
                            duplicateFilesList.Add(dupEx.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        var currentCountNow = Interlocked.Increment(ref currentCount);
                        var currentProgress = currentCountNow / (float)totalCount;
                        Workspace.SetProgressThreadSafe(currentProgress, "Skipping " + Path.GetFileName(fileName));

                        lock (stackTraceSb)
                        {
                            stackTraceSb.AppendLine(ex.ToString());
                        }
                    }
                }
            });

            if (anyLoaded)
                Workspace.SetProgressThreadSafe(1f, "Done");
            else
                Workspace.SetProgressThreadSafe(1f, "All files skipped, nothing loaded");

            Workspace.ModifyMutex.ReleaseMutex();
        });

        if (duplicateFilesList.Count > 0 || stackTraceSb.Length > 0)
        {
            var fullErrorSb = new StringBuilder();
            if (duplicateFilesList.Count > 0)
            {
                fullErrorSb.AppendLine("Duplicate files skipped:");
                foreach (var duplicateFileInfo in duplicateFilesList)
                {
                    fullErrorSb.AppendLine($"- {duplicateFileInfo.DisplayLine}");
                }
            }

            if (stackTraceSb.Length > 0)
            {
                fullErrorSb.AppendLine("Exceptions from files that failed to load:");
                fullErrorSb.Append(stackTraceSb);
            }

            var fullErrorStr = fullErrorSb.ToString().TrimEnd('\r', '\n');
            await MessageBoxUtil.ShowDialog("Some files failed to load", fullErrorStr);
        }

        // load class database for these files (or request user to provide one)
        if (Workspace.Manager.ClassDatabase is null)
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
                if (version is not null)
                {
                    Workspace.Manager.LoadClassDatabaseFromPackage(version);
                }
            }
        }

        if (Workspace.Manager.ClassDatabase is not null && databaseStartedUnloaded)
        {
            var cldbVersion = Workspace.Manager.ClassDatabase.Header.Version.ToString();
            WindowTitle = $"{DefaultWindowTitle} (workspace ver {cldbVersion})";
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
            FileTypeFilter = [
                new FilePickerFileType("All files (*.*)") { Patterns = [ "*" ] }
            ],
            AllowMultiple = true
        });

        var fileNames = FileDialogUtils.GetOpenFileDialogFiles(result);
        await OpenFiles(fileNames);
    }

    public async void FileOpenFolder()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open a folder",
            AllowMultiple = true
        });

        var folderNames = FileDialogUtils.GetOpenFolderDialogFolders(result);
        await OpenFiles(folderNames);
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
        var wsItems = GetSelectedDocWorkspaceItems();
        if (wsItems is null)
            return;

        await DoSaveOverwrite(wsItems);
    }

    // more like "save copy as"
    public async Task FileSaveAs()
    {
        var wsItems = GetSelectedDocWorkspaceItems();
        if (wsItems is null)
            return;

        await DoSaveCopy(wsItems);
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

        foreach (var dockable in _factory.DocMan.Documents)
        {
            _factory.CloseDockable(dockable);
        }

        _factory.DocMan.Documents.Clear();

        WindowTitle = DefaultWindowTitle;
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

    public List<AssetsFileInstance> GetSelectedDocFileInsts()
    {
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

        return fileInsts;
    }

    private IEnumerable<WorkspaceItem>? GetSelectedDocWorkspaceItems()
    {
        var lastFocusedDoc = _factory.DocMan.LastFocusedDocument;
        if (lastFocusedDoc is not AssetDocumentViewModel assetDocVm)
            return null;

        var wsItems = assetDocVm.FileInsts
            .Select(Workspace.FindWorkspaceItemByInstance)
            .Where(i => i is not null) as IEnumerable<WorkspaceItem>;

        return wsItems;
    }

    private async Task<AssetDocumentViewModel?> OpenAssetDocument(List<WorkspaceItem> workspaceItems, bool replaceDock)
    {
        var loadContainers = ConfigurationManager.Settings.LoadContainerPaths;

        AssetDocumentViewModel document;
        if (workspaceItems.Count == 1)
        {
            var workspaceItem = workspaceItems[0];

            if (workspaceItem.ObjectType != WorkspaceItemType.AssetsFile)
                return null;

            if (workspaceItem.Object is not AssetsFileInstance mainFileInst)
                return null;

            document = new AssetDocumentViewModel(Workspace, loadContainers)
            {
                Title = mainFileInst.name,
                Id = mainFileInst.name
            };

            _lastLoadedFiles = [mainFileInst];
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

            document = new AssetDocumentViewModel(Workspace, loadContainers)
            {
                Title = $"{mainFileInst.name} and {assetsFileItems.Count - 1} other files"
            };

            _lastLoadedFiles = assetsFileItems!;
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
                if (oldDockable is Document oldDoc)
                {
                    _factory.DocMan.Documents.Remove(oldDoc);
                }
            }
            else
            {
                _factory.AddDockable(files, document);
            }

            _factory.SetActiveDockable(document);
            _factory.SetFocusedDockable(files, document);
            _factory.DocMan.Documents.Add(document);
            _factory.DocMan.LastFocusedDocument = document;
        }

        _lastLoadedFiles = workspaceItems.Select(i => i.Object as AssetsFileInstance).Where(i => i != null).ToList()!;
        await document.Load(_lastLoadedFiles);

        return document;
    }

    private async void ShowEditAssetDialog(AssetInst asset)
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

    private async void OnRequestCloseFile(object recipient, RequestCloseFileMessage message)
    {
        var wsItem = message.Value;
        if (wsItem.Parent is not null)
            return;

        List<AssetsFileInstance> fileInsts;
        if (wsItem.Object is BundleFileInstance bunInst)
        {
            fileInsts = wsItem.Children.Select(i => i.Object).OfType<AssetsFileInstance>().ToList();
        }
        else if (wsItem.Object is AssetsFileInstance fileInst)
        {
            fileInsts = [fileInst];
        }
        else
        {
            fileInsts = [];
        }

        foreach (var dockable in _factory.DocMan.Documents)
        {
            if (dockable is not AssetDocumentViewModel document)
                continue;

            var intersectCount = document.FileInsts.Intersect(fileInsts).Count();
            var matchesAny = intersectCount > 0;
            var matchesAll = intersectCount == document.FileInsts.Count;
            if (matchesAll)
            {
                // todo: reload the dockable without the closed items
                // or just delete only the closed items
                _factory.CloseDockable(dockable);
            }
            else if (matchesAny)
            {
                var newFileInsts = document.FileInsts.Except(fileInsts).ToList();
                await document.Load(newFileInsts);
            }
        }

        Workspace.Close(wsItem);
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

    public void ShowAssetInfoDialog()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");

        if (explorer is null)
        {
            return;
        }

        var fileInsts = GetSelectedDocFileInsts();
        var wsItems = fileInsts
            .Select(Workspace.FindWorkspaceItemByInstance)
            .Where(i => i is not null) as IEnumerable<WorkspaceItem>;
        dialogService.Show(new AssetInfoViewModel(Workspace, wsItems));
    }

    public void ShowSearchBytesDialog()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var explorer = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");

        if (explorer is null)
        {
            return;
        }

        var fileInsts = GetSelectedDocFileInsts();
        dialogService.Show(new AssetDataSearchViewModel(Workspace, fileInsts));
    }

    public void ShowOptionsDialog()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        dialogService.Show(new SettingsViewModel());
    }
}
