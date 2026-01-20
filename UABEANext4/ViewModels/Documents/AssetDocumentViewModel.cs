using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using DynamicData.Binding;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Configuration;
using UABEANext4.Logic.ImportExport;
using UABEANext4.Plugins;
using UABEANext4.Services;
using UABEANext4.Util;
using UABEANext4.ViewModels.Dialogs;
using UABEANext4.ViewModels.Menu;

namespace UABEANext4.ViewModels.Documents;

public partial class AssetDocumentViewModel : Document
{
    const string TOOL_TITLE = "Asset Document";

    public Workspace Workspace { get; }
    public bool LoadContainers { get; }

    public List<AssetInst> SelectedItems { get; set; } = [];
    public List<AssetsFileInstance> FileInsts { get; set; } = [];

    public ReadOnlyObservableCollection<AssetInst> Items { get; set; } = new([]);

    public Dictionary<AssetClassID, string> ClassIdToString { get; }

    [ObservableProperty]
    public DataGridCollectionView _collectionView = new(new List<object>());
    [ObservableProperty]
    public string _searchText = "";
    [ObservableProperty]
    public ObservableCollection<PluginItemInfo> _pluginsItems = [];

    [ObservableProperty]
    public bool _isSearchCaseSensitive = false;
    [ObservableProperty]
    public AssetTextSearchKind _searchKind = 0;

    [ObservableProperty]
    public ObservableCollection<MenuOptionViewModel> _contextMenuItems = [];

    public event Action? ShowPluginsContextMenuAction;
    public event Action<List<AssetInst>>? SetSelectedItemsAction;
    [ObservableProperty]
    private bool _isBusy;

    private List<TypeFilterTypeEntry>? _filterTypes = null;
    private HashSet<TypeFilterTypeEntry> _filterTypesFiltered = [];
    private Dictionary<AssetsFileInstance, AssetTypeReference?[]> _typeRefLookup = [];
    private readonly Action<string> _setDataGridFilterDb;

    private IDisposable? _disposableLastList;
    private CancellationTokenSource? _loadCtSrc;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public AssetDocumentViewModel()
    {
        Workspace = new();
        LoadContainers = false;

        ClassIdToString = Enum
            .GetValues(typeof(AssetClassID))
            .Cast<AssetClassID>()
            .ToDictionary(enm => enm, enm => enm.ToString());

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _setDataGridFilterDb = DebounceUtils.Debounce<string>((searchText) =>
        {
            CollectionView.Filter = SetDataGridFilter(searchText);
        }, 300);
    }

    public AssetDocumentViewModel(Workspace workspace, bool loadContainers)
    {
        Workspace = workspace;
        LoadContainers = loadContainers;
        ClassIdToString = Enum
            .GetValues(typeof(AssetClassID))
            .Cast<AssetClassID>()
            .ToDictionary(enm => enm, enm => enm.ToString());

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _setDataGridFilterDb = DebounceUtils.Debounce<string>((searchText) =>
        {
            CollectionView.Filter = SetDataGridFilter(searchText);
        }, 300);

        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, (r, h) => _ = OnWorkspaceClosing(r, h));
    }

    partial void OnSearchTextChanged(string value) => _setDataGridFilterDb(value);

    private Func<object, bool> SetDataGridFilter(string searchText)
    {
        var strCmp = IsSearchCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        Regex? searchRegex;
        try
        {
            searchRegex = SearchKind == AssetTextSearchKind.RegexSearch
                ? new Regex(searchText, IsSearchCaseSensitive
                    ? RegexOptions.None
                    : RegexOptions.IgnoreCase)
                : null;
        }
        catch
        {
            // skip invalid regex
            searchRegex = null;
        }

        if (_filterTypesFiltered is null || _filterTypesFiltered.Count == 0)
        {
            // don't need to filter on types, use simpler branch

            if (string.IsNullOrEmpty(searchText))
                return a => true;

            if (searchRegex is not null)
            {
                // simple + regex
                return o =>
                {
                    if (o is not AssetInst a)
                        return false;

                    if (searchRegex.IsMatch(a.DisplayName))
                        return true;

                    if (ClassIdToString.TryGetValue(a.Type, out string? classIdName) && searchRegex.IsMatch(classIdName))
                        return true;
                    
                    if (searchRegex.IsMatch(a.PathId.ToString()))
                        return true;

                    return false;
                };
            }
            else
            {
                // simple + no regex
                return o =>
                {
                    if (o is not AssetInst a)
                        return false;

                    if (a.DisplayName.Contains(searchText, strCmp))
                        return true;

                    if (ClassIdToString.TryGetValue(a.Type, out string? classIdName) && classIdName == searchText)
                        return true;
                    
                    if (a.PathId.ToString().Contains(searchText, strCmp))
                        return true;

                    return false;
                };
            }
        }
        else
        {
            // need to filter on types

            // allocate one object and overwrite its fields
            var baseTypeEntry = new TypeFilterTypeEntry
            {
                DisplayText = string.Empty,
                TypeId = 0,
                ScriptRef = null
            };

            if (searchRegex is not null)
            {
                // type + regex
                return o =>
                {
                    if (o is not AssetInst a)
                        return false;

                    // require a match on name / class id / path id first
                    if (!searchRegex.IsMatch(a.DisplayName))
                    {
                        if (!(ClassIdToString.TryGetValue(a.Type, out string? classIdName) && searchRegex.IsMatch(classIdName))
                            && !searchRegex.IsMatch(a.PathId.ToString()))
                        {
                            return false;
                        }
                    }

                    return DoesTypeFilterPass(a, baseTypeEntry);
                };
            }
            else
            {
                // type + no regex
                return o =>
                {
                    if (o is not AssetInst a)
                        return false;

                    // require a match on name / class id / path id first
                    if (!a.DisplayName.Contains(searchText, strCmp))
                    {
                        if (!(ClassIdToString.TryGetValue(a.Type, out string? classIdName) && classIdName == searchText)
                            && !a.PathId.ToString().Contains(searchText, strCmp))
                        {
                            return false;
                        }
                    }

                    return DoesTypeFilterPass(a, baseTypeEntry);
                };
            }
        }
    }

    private bool DoesTypeFilterPass(AssetInst assetInst, TypeFilterTypeEntry baseTypeEntry)
    {
        var scriptIndex = assetInst.GetScriptIndex(assetInst.FileInstance.file);

        baseTypeEntry.TypeId = assetInst.TypeId;
        if (baseTypeEntry.TypeId < 0)
        {
            baseTypeEntry.TypeId = (int)AssetClassID.MonoBehaviour;
        }

        if (scriptIndex != ushort.MaxValue)
        {
            var typeList = _typeRefLookup[assetInst.FileInstance];

            // just in case, let's check the bounds here
            if (scriptIndex < typeList.Length)
                baseTypeEntry.ScriptRef = typeList[scriptIndex];
            else
                baseTypeEntry.ScriptRef = null;
        }
        else
        {
            baseTypeEntry.ScriptRef = null;
        }

        return _filterTypesFiltered.Contains(baseTypeEntry);
    }

    public async Task Load(List<AssetsFileInstance> fileInsts)
    {
        if (Workspace == null)
            return;

        _disposableLastList?.Dispose();

        var sourceList = new SourceList<RangeObservableCollection<AssetFileInfo>>();

        _loadCtSrc?.Cancel();
        _loadCtSrc = new CancellationTokenSource();
        var loadCt = _loadCtSrc.Token;
        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                foreach (var fileInst in fileInsts)
                {
                    if (loadCt.IsCancellationRequested)
                        loadCt.ThrowIfCancellationRequested();

                    var infosObsCol = (RangeObservableCollection<AssetFileInfo>)fileInst.file.Metadata.AssetInfos;
                    sourceList.Add(infosObsCol);

                    if (LoadContainers)
                        LoadContainersIntoInfos(fileInst, infosObsCol);
                }
            }, loadCt);


            var observableList = sourceList
                .Connect()
                .MergeMany(e => e.ToObservableChangeSet())
                .Transform(a => (AssetInst)a);

            _disposableLastList = observableList.Bind(out var items).Subscribe();
            Items = items;
            FileInsts = fileInsts;

            _filterTypes = null;
            _filterTypesFiltered = [];
            _typeRefLookup = [];

            CollectionView = new DataGridCollectionView(Items);
            CollectionView.Filter = SetDataGridFilter(SearchText);
        }
        catch (OperationCanceledException)
        {
            sourceList.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadContainersIntoInfos(AssetsFileInstance fileInst, IList<AssetFileInfo> fileInfos)
    {
        ContainerTool? contToolRes = null;
        AssetsFileInstance? contFile; // file that stores container mapping for this file
        AssetTypeValueField? contBf; // base field that stores container mapping
        if (ContainerTool.TryGetBundleContainerBaseField(Workspace, fileInst, out contFile, out contBf))
        {
            contToolRes = ContainerTool.FromAssetBundle(Workspace.Manager, contFile, contBf);
        }
        else if (ContainerTool.TryGetRsrcManContainerBaseField(Workspace, fileInst, out contFile, out contBf))
        {
            contToolRes = ContainerTool.FromResourceManager(Workspace.Manager, contFile, contBf);
        }

        if (contToolRes is null)
            return;

        foreach (var assetInf in fileInfos)
        {
            var assetPtr = new AssetPPtr(fileInst.path, assetInf.PathId);
            var path = contToolRes.GetContainerPath(assetPtr);
            if (path is not null && assetInf is AssetInst asset)
            {
                asset.DisplayContainer = path;
            }
        }
    }

    public async void ViewScene()
    {
        if (SelectedItems.Count >= 1)
        {
            var asset = SelectedItems.First();

            // select gameobject if this is a component
            if (asset.Type != AssetClassID.GameObject)
            {
                var assetBf = Workspace.GetBaseField(asset);
                if (assetBf is null)
                {
                    await MessageBoxUtil.ShowDialog("Read error", "Tried to check for component fields but couldn't deserialize the asset.");
                    return;
                }

                var assetBfGoPtr = assetBf["m_GameObject"];
                if (assetBfGoPtr.IsDummy)
                {
                    await MessageBoxUtil.ShowDialog("Not a GameObject or component", "The selected asset must be a GameObject or GameObject component.");
                    return;
                }

                asset = Workspace.GetAssetInst(asset.FileInstance, assetBfGoPtr);
                if (asset is null)
                {
                    await MessageBoxUtil.ShowDialog("Invalid GameObject reference", "Can't find component's GameObject. Do you need to load a dependency?");
                    return;
                }
            }

            WeakReferenceMessenger.Default.Send(new RequestSceneViewMessage(asset));
        }
    }

    [RelayCommand]
    public async Task Import()
    {
        if (SelectedItems.Count > 1)
        {
            await ImportBatch(SelectedItems.ToList());
        }
        else if (SelectedItems.Count == 1)
        {
            await ImportSingle(SelectedItems.First());
        }
    }

    public async Task ImportBatch(List<AssetInst> assets)
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
            return;

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folder to import",
            AllowMultiple = false
        });

        var folders = FileDialogUtils.GetOpenFolderDialogFolders(result);
        if (folders == null || folders.Length != 1)
            return;

        var exts = new List<string> { "json", "txt", "dat" };
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();

        var batchInfos = await dialogService.ShowDialog(new BatchImportViewModel(Workspace, assets, folders[0], exts));
        if (batchInfos == null)
            return;

        var fileNamesToDirty = new HashSet<string>();

        try
        {
            IsBusy = true;

            await Task.Run(async () =>
            {
                foreach (var batchInfo in batchInfos)
                {
                    var selectedFilePath = batchInfo.ImportFile;
                    if (selectedFilePath == null) continue;

                    var selectedAsset = batchInfo.Asset;
                    var selectedInst = selectedAsset.FileInstance;

                    Workspace.CheckAndSetMonoTempGenerators(selectedInst, selectedAsset);

                    using FileStream fs = File.OpenRead(selectedFilePath);
                    var importer = new AssetImport(fs, Workspace.Manager.GetRefTypeManager(selectedInst));

                    byte[]? data;
                    string? exceptionMessage;

                    if (selectedFilePath.EndsWith(".json"))
                    {
                        var tempField = Workspace.GetTemplateField(selectedAsset);
                        data = importer.ImportJsonAsset(tempField, out exceptionMessage);
                    }
                    else if (selectedFilePath.EndsWith(".txt"))
                    {
                        data = importer.ImportTextAsset(out exceptionMessage);
                    }
                    else
                    {
                        exceptionMessage = string.Empty;
                        data = importer.ImportRawAsset();
                    }

                    if (data == null)
                    {
                        await Dispatcher.UIThread.InvokeAsync(async () =>
                        {
                            await MessageBoxUtil.ShowDialog("Parse error",
                                $"Error in file {Path.GetFileName(selectedFilePath)}:\n{exceptionMessage}");
                        });
                        continue;
                    }

                    selectedAsset.UpdateAssetDataAndRow(Workspace, data);

                    lock (fileNamesToDirty)
                    {
                        fileNamesToDirty.Add(selectedAsset.FileInstance.name);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Import Error", "An error occurred: " + ex.Message);
        }
        finally
        {
            foreach (var fileName in fileNamesToDirty)
            {
                if (Workspace.ItemLookup.TryGetValue(fileName, out var fileToDirty))
                {
                    Workspace.Dirty(fileToDirty);
                }
            }
            IsBusy = false;
        }
    }

    public async Task ImportSingle(AssetInst asset)
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
            return;

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose file to import",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
            new FilePickerFileType("UABEA json dump (*.json)") { Patterns = new[] { "*.json" } },
            new FilePickerFileType("UABE txt dump (*.txt)") { Patterns = new[] { "*.txt" } },
            new FilePickerFileType("Raw dump (*.dat)") { Patterns = new[] { "*.dat" } },
            new FilePickerFileType("All files (*.*)") { Patterns = new[] { "*" } },
        },
        });

        var files = FileDialogUtils.GetOpenFileDialogFiles(result);
        if (files == null || files.Length == 0)
            return;

        var file = files[0];

        try
        {
            IsBusy = true;

            await Task.Run(async () =>
            {
                Workspace.CheckAndSetMonoTempGenerators(asset.FileInstance, asset);

                using var fs = File.OpenRead(file);
                var importer = new AssetImport(fs, Workspace.Manager.GetRefTypeManager(asset.FileInstance));

                byte[]? data = null;
                string? exception = null;

                if (file.EndsWith(".json"))
                {
                    var baseField = Workspace.GetTemplateField(asset);
                    data = importer.ImportJsonAsset(baseField, out exception);
                }
                else if (file.EndsWith(".txt"))
                {
                    data = importer.ImportTextAsset(out exception);
                }
                else
                {
                    data = importer.ImportRawAsset();
                }

                if (data != null)
                {
                    asset.UpdateAssetDataAndRow(Workspace, data);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var fileToDirty = Workspace.ItemLookup[asset.FileInstance.name];
                        Workspace.Dirty(fileToDirty);
                    });
                }
                else if (exception != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await MessageBoxUtil.ShowDialog("Import Error", exception);
                    });
                }
            });
        }
        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Import Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task Export()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var maxNameLen = ConfigurationManager.Settings.ExportNameLength;
        var filesToWrite = new List<(AssetInst Asset, string Path)>();

        if (SelectedItems.Count > 1)
        {
            var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
            var exportType = await dialogService.ShowDialog(new SelectDumpViewModel(true));
            if (exportType == null)
            {
                return;
            }

            await Task.Yield();

            var exportExt = exportType switch
            {
                SelectedDumpType.JsonDump => ".json",
                SelectedDumpType.TxtDump => ".txt",
                SelectedDumpType.RawDump => ".dat",
                _ => ".dat"
            };

            var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose folder to export to",
                AllowMultiple = false
            });

            var folders = FileDialogUtils.GetOpenFolderDialogFolders(result);
            if (folders.Length == 0)
            {
                return;
            }

            var folder = folders[0];
            foreach (var asset in SelectedItems)
            {
                var exportFileName = AssetNamer.GetAssetFileName(Workspace, asset, exportExt, maxNameLen);
                var exportFilePath = Path.Combine(folder, exportFileName);
                filesToWrite.Add((asset, exportFilePath));
            }
        }
        else if (SelectedItems.Count == 1)
        {
            var asset = SelectedItems.First();
            var exportFileName = AssetNamer.GetAssetFileName(Workspace, asset, string.Empty, maxNameLen);

            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Choose file to export",
                FileTypeChoices = new[]
                {
                new FilePickerFileType("UABEA json dump (*.json)") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("UABE txt dump (*.txt)") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("Raw dump (*.dat)") { Patterns = new[] { "*.dat" } }
            },
                DefaultExtension = "json",
                SuggestedFileName = exportFileName
            });

            var file = FileDialogUtils.GetSaveFileDialogFile(result);
            if (file == null)
            {
                return;
            }

            filesToWrite.Add((asset, file));
        }

        if (filesToWrite.Count == 0)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await Task.Run(() =>
            {
                foreach (var (asset, file) in filesToWrite)
                {
                    using var fs = File.Open(file, FileMode.Create, FileAccess.Write, FileShare.None);
                    var exporter = new AssetExport(fs);

                    if (file.EndsWith(".json") || file.EndsWith(".txt"))
                    {
                        var baseField = Workspace.GetBaseField(asset);
                        if (baseField == null)
                        {
                            byte[] failMsg = Encoding.UTF8.GetBytes("Asset failed to deserialize.");
                            fs.Write(failMsg, 0, failMsg.Length);
                        }
                        else
                        {
                            if (file.EndsWith(".json"))
                                exporter.DumpJsonAsset(baseField);
                            else
                                exporter.DumpTextAsset(baseField);
                        }
                    }
                    else if (file.EndsWith(".dat"))
                    {
                        if (asset.IsReplacerPreviewable)
                        {
                            var previewStream = asset.Replacer.GetPreviewStream();
                            var previewReader = new AssetsFileReader(previewStream);
                            lock (previewStream)
                            {
                                exporter.DumpRawAsset(previewReader, 0, (uint)previewStream.Length);
                            }
                        }
                        else
                        {
                            lock (asset.FileInstance.LockReader)
                            {
                                exporter.DumpRawAsset(asset.FileReader, asset.AbsoluteByteStart, asset.ByteSize);
                            }
                        }
                    }
                }
            });
        }

        catch (Exception ex)
        {
            await MessageBoxUtil.ShowDialog("Export Error", $"Failed to export assets: {ex.Message}");
        }

        finally
        {
            IsBusy = false;
        }
    }

    public void ShowPlugins()
    {
        if (SelectedItems.Count == 0)
        {
            PluginsItems.Clear();
            PluginsItems.Add(new PluginItemInfo("No assets selected", null, this));
            return;
        }

        var pluginTypes = UavPluginMode.Export | UavPluginMode.Import;
        var pluginsList = Workspace.Plugins.GetOptionsThatSupport(Workspace, SelectedItems, pluginTypes);
        if (pluginsList == null)
        {
            return;
        }

        if (pluginsList.Count == 0)
        {
            PluginsItems.Clear();
            PluginsItems.Add(new PluginItemInfo("No plugins available", null, this));
        }
        else
        {
            PluginsItems.Clear();
            foreach (var plugin in pluginsList)
            {
                PluginsItems.Add(new PluginItemInfo(plugin.Option.Name, plugin.Option, this));
            }
        }

        ShowPluginsContextMenuAction?.Invoke();
    }

    public void EditDump()
    {
        if (SelectedItems.Count > 0)
        {
            WeakReferenceMessenger.Default.Send(new RequestEditAssetMessage(SelectedItems[^1]));
        }
    }

    public async void AddAsset()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();
        var result = await dialogService.ShowDialog(new AddAssetViewModel(Workspace, FileInsts));
        if (result == null)
        {
            return;
        }

        var baseInfo = AssetFileInfo.Create(
            result.File.file, result.PathId, result.TypeId, result.ScriptIndex,
            Workspace.Manager.ClassDatabase, false
        );
        var info = new AssetInst(result.File, baseInfo);
        var baseField = ValueBuilder.DefaultValueFieldFromTemplate(result.TempField);

        result.File.file.Metadata.AddAssetInfo(info);
        info.UpdateAssetDataAndRow(Workspace, baseField);
    }

    public async void RemoveAsset()
    {
        if (SelectedItems.Count == 0)
            return;

        var singPlurStr = SelectedItems.Count > 1
            ? "these assets"
            : "this asset";

        var dialogRes = await MessageBoxUtil.ShowDialog(
            "Remove asset",
            $"Are you sure you want to remove {singPlurStr}? Remaining references to {singPlurStr} will not be fixed.",
            MessageBoxType.YesNo
        );
        if (dialogRes == MessageBoxResult.No)
            return;

        var modifiedFileInsts = new HashSet<AssetsFileInstance>();
        foreach (var selectedItem in SelectedItems)
        {
            var assetFileInst = selectedItem.FileInstance;
            assetFileInst.file.Metadata.RemoveAssetInfo(selectedItem);
            modifiedFileInsts.Add(assetFileInst);
        }

        foreach (var modifiedFileInst in modifiedFileInsts)
        {
            var wsItem = Workspace.FindWorkspaceItemByInstance(modifiedFileInst);
            if (wsItem is not null)
            {
                Workspace.Dirty(wsItem);
            }
        }
    }

    public async void SetTypeFilter()
    {
        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();

        // if not generated already, find all unique types to filter on
        _filterTypes ??= SelectTypeFilterViewModel.MakeTypeFilterTypes(Workspace, Items);

        var result = await dialogService.ShowDialog(new SelectTypeFilterViewModel(_filterTypes));
        if (result == null)
            return;

        // set filtered list
        _filterTypesFiltered = result.ToHashSet();

        // also generate a list of file instance + script index -> actual script for quick lookup
        if (_typeRefLookup.Count == 0)
        {
            foreach (var fileInst in FileInsts)
            {
                var scriptTypes = fileInst.file.Metadata.ScriptTypes;
                var scriptRefArray = new AssetTypeReference?[scriptTypes.Count];

                for (int i = 0; i < scriptTypes.Count; i++)
                {
                    AssetTypeReference typeRef = AssetHelper.GetAssetsFileScriptInfo(Workspace.Manager, fileInst, i);
                    if (typeRef == null)
                    {
                        scriptRefArray[i] = null;
                        continue;
                    }

                    scriptRefArray[i] = typeRef;
                }

                _typeRefLookup[fileInst] = scriptRefArray;
            }
        }

        // reload filter
        CollectionView.Filter = SetDataGridFilter(SearchText);
    }

    public void SetSelectedItems(List<AssetInst> assets)
    {
        SetSelectedItemsAction?.Invoke(assets);
    }

    public void OnAssetOpened(List<AssetInst> assets)
    {
        if (assets.Count > 0)
        {
            WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage([assets[0]]));
        }

        SelectedItems = assets;
    }

    public void ResendSelectedAssetsSelected()
    {
        if (SelectedItems.Count > 0)
        {
            WeakReferenceMessenger.Default.Send(new AssetsSelectedMessage([SelectedItems[0]]));
        }
    }

    public void CreateContextMenu()
    {
        ContextMenuItems.Clear();
        if (SelectedItems.Count == 0)
            return;
        var selected = SelectedItems;
        var first = selected[0];

        ContextMenuItems.Add(new MenuOptionViewModel("Edit Data",
            new RelayCommand(EditDump), null, ApplicationExtensions.GetInconPath("action-view-info.png")));

        ContextMenuItems.Add(new MenuOptionViewModel("-"));

        var pluginsMenu = new MenuOptionViewModel("Plugins", null, null, ApplicationExtensions.GetInconPath("action-plugins.png"))
        {
            Items = new ObservableCollection<MenuOptionViewModel>()
        };

        var pluginTypes = UavPluginMode.Export | UavPluginMode.Import;
        var pluginsList = Workspace.Plugins.GetOptionsThatSupport(Workspace, SelectedItems, pluginTypes);

        if (pluginsList != null && pluginsList.Count > 0)
        {
            foreach (var plugin in pluginsList)
            {
                pluginsMenu.Items.Add(new MenuOptionViewModel(
                    plugin.Option.Name,
                    new RelayCommand(() => plugin.Option.Execute(Workspace, new UavPluginFunctions(), pluginTypes, SelectedItems))
                ));
            }
        }
        else
        {
            var disabledItem = new MenuOptionViewModel("No plugins available");
            pluginsMenu.Items.Add(disabledItem);
        }
        ContextMenuItems.Add(pluginsMenu);
        ContextMenuItems.Add(new MenuOptionViewModel("-"));

        var copyMenu = new MenuOptionViewModel("Copy");
        copyMenu.Items = new ObservableCollection<MenuOptionViewModel>
        {
            new MenuOptionViewModel("Name", new AsyncRelayCommand(() => ApplicationExtensions.CopyToClipboard(first.DisplayName))),
            new MenuOptionViewModel("Path ID", new AsyncRelayCommand(() => ApplicationExtensions.CopyToClipboard(first.PathId.ToString())))
        };
        ContextMenuItems.Add(copyMenu);

        ContextMenuItems.Add(new MenuOptionViewModel("Remove",
            new RelayCommand(RemoveAsset), null, ApplicationExtensions.GetInconPath("action-remove-asset.png")));
    }

    private async Task OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        await Load([]);
    }
}

public enum AssetTextSearchKind
{
    PlainSearch,
    RegexSearch
}