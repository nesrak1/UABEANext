using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
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
using System.Threading;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.ImportExport;
using UABEANext4.Plugins;
using UABEANext4.Services;
using UABEANext4.Util;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.ViewModels.Documents;
public partial class AssetDocumentViewModel : Document
{
    const string TOOL_TITLE = "Asset Document";

    public Workspace Workspace { get; }

    public List<AssetInst> SelectedItems { get; set; }
    public List<AssetsFileInstance> FileInsts { get; set; }

    public ReadOnlyObservableCollection<AssetInst> Items { get; set; }

    public Dictionary<AssetClassID, string> ClassIdToString { get; }

    [ObservableProperty]
    public DataGridCollectionView _collectionView;
    [ObservableProperty]
    public string _searchText = "";
    [ObservableProperty]
    public ObservableCollection<PluginItemInfo> _pluginsItems;

    public event Action? ShowPluginsContextMenu;

    private readonly Action<string> _setDataGridFilterDb;

    private IDisposable? _disposableLastList;
    private CancellationToken? _cancellationToken;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public AssetDocumentViewModel()
    {
        Workspace = new();
        SelectedItems = new();
        FileInsts = new();
        Items = new(new ObservableCollection<AssetInst>());
        CollectionView = new DataGridCollectionView(new List<object>());
        ClassIdToString = Enum
            .GetValues(typeof(AssetClassID))
            .Cast<AssetClassID>()
            .ToDictionary(enm => enm, enm => enm.ToString());

        PluginsItems = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _setDataGridFilterDb = DebounceUtils.Debounce<string>((searchText) =>
        {
            CollectionView.Filter = SetDataGridFilter(searchText);
        }, 300);
    }

    public AssetDocumentViewModel(Workspace workspace)
    {
        Workspace = workspace;
        SelectedItems = new();
        FileInsts = new();
        Items = new(new ObservableCollection<AssetInst>());
        CollectionView = new DataGridCollectionView(new List<object>());
        ClassIdToString = Enum
            .GetValues(typeof(AssetClassID))
            .Cast<AssetClassID>()
            .ToDictionary(enm => enm, enm => enm.ToString());

        PluginsItems = new();

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
        if (string.IsNullOrEmpty(searchText))
            return a => true;

        return o =>
        {
            if (o is not AssetInst a)
                return false;

            if (a.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            if (ClassIdToString.TryGetValue(a.Type, out string? classIdName) && classIdName == searchText)
                return true;

            return false;
        };
    }

    public async Task Load(List<AssetsFileInstance> fileInsts)
    {
        if (Workspace == null)
            return;

        _disposableLastList?.Dispose();

        var sourceList = new SourceList<RangeObservableCollection<AssetFileInfo>>();
        var tasks = new List<Task>();
        _cancellationToken = new CancellationToken();
        await Task.Run(() =>
        {
            foreach (var fileInst in fileInsts)
            {
                var infosObsCol = (RangeObservableCollection<AssetFileInfo>)fileInst.file.Metadata.AssetInfos;
                sourceList.Add(infosObsCol);
            }
        }, _cancellationToken.Value);

        var observableList = sourceList
            .Connect()
            .MergeMany(e => e.ToObservableChangeSet())
            .Transform(a => (AssetInst)a);

        _disposableLastList = observableList.Bind(out var items).Subscribe();
        Items = items;
        FileInsts = fileInsts;

        CollectionView = new DataGridCollectionView(Items);
        CollectionView.Filter = SetDataGridFilter(SearchText);
    }

    public void ViewScene()
    {
        if (SelectedItems.Count >= 1)
        {
            WeakReferenceMessenger.Default.Send(new RequestSceneViewMessage(SelectedItems.First()));
        }
    }

    public void Import()
    {
        if (SelectedItems.Count > 1)
        {
            ImportBatch(SelectedItems.ToList());
        }
        else if (SelectedItems.Count == 1)
        {
            ImportSingle(SelectedItems.First());
        }
    }

    public async void ImportBatch(List<AssetInst> assets)
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose folder to import",
            AllowMultiple = false
        });

        var folders = FileDialogUtils.GetOpenFolderDialogFolders(result);
        if (folders == null || folders.Length != 1)
            return;

        List<string> exts = new List<string>()
        {
            "json", "txt", "dat"
        };

        var dialogService = Ioc.Default.GetRequiredService<IDialogService>();

        var fileNamesToDirty = new HashSet<string>();
        var batchInfos = await dialogService.ShowDialog(new BatchImportViewModel(Workspace, assets, folders[0], exts));
        if (batchInfos == null)
        {
            return;
        }

        foreach (ImportBatchInfo batchInfo in batchInfos)
        {
            var selectedFilePath = batchInfo.ImportFile;
            if (selectedFilePath == null)
                continue;

            var selectedAsset = batchInfo.Asset;
            var selectedInst = selectedAsset.FileInstance;

            using FileStream fs = File.OpenRead(selectedFilePath);

            Workspace.CheckAndSetMonoTempGenerators(selectedInst, selectedAsset);
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
                await MessageBoxUtil.ShowDialog("Parse error", "Something went wrong when reading the dump file:\n" + exceptionMessage);
                goto dirtyFiles;
            }

            selectedAsset.UpdateAssetDataAndRow(Workspace, data);
            fileNamesToDirty.Add(selectedAsset.FileInstance.name);
        }

    dirtyFiles:
        foreach (var fileName in fileNamesToDirty)
        {
            var fileToDirty = Workspace.ItemLookup[fileName];
            Workspace.Dirty(fileToDirty);
        }
    }

    public async void ImportSingle(AssetInst asset)
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose file to import",
            AllowMultiple = true,
            FileTypeFilter = new FilePickerFileType[]
            {
                new FilePickerFileType("UABEA json dump (*.json)") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("UABE txt dump (*.txt)") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("Raw dump (*.dat)") { Patterns = new[] { "*.dat" } },
                new FilePickerFileType("Raw dump (*.*)") { Patterns = new[] { "*" } },
            },
        });

        var files = FileDialogUtils.GetOpenFileDialogFiles(result);
        if (files == null || files.Length == 0)
            return;

        var file = files[0];
        using var fs = File.OpenRead(file);

        Workspace.CheckAndSetMonoTempGenerators(asset.FileInstance, asset);
        var importer = new AssetImport(fs, Workspace.Manager.GetRefTypeManager(asset.FileInstance));

        byte[]? data = null;
        string? exception;

        if (file.EndsWith(".json") || file.EndsWith(".txt"))
        {
            if (file.EndsWith(".json"))
            {
                var baseField = Workspace.GetTemplateField(asset);
                if (baseField != null)
                {
                    data = importer.ImportJsonAsset(baseField, out exception);
                }
                else
                {
                    // handle template read error
                }
            }
            else if (file.EndsWith(".txt"))
            {
                data = importer.ImportTextAsset(out exception);
            }
        }
        else //if (file.EndsWith(".dat"))
        {
            using var stream = File.OpenRead(file);
            data = importer.ImportRawAsset();
        }

        if (data != null)
        {
            asset.UpdateAssetDataAndRow(Workspace, data);
        }

        var fileToDirty = Workspace.ItemLookup[asset.FileInstance.name];
        Workspace.Dirty(fileToDirty);
    }

    public async void Export()
    {
        var storageProvider = StorageService.GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var filesToWrite = new List<(AssetInst, string)>();
        if (SelectedItems.Count > 1)
        {
            var dialogService = Ioc.Default.GetRequiredService<IDialogService>();

            var exportType = await dialogService.ShowDialog(new SelectDumpViewModel(true));
            if (exportType == null)
            {
                return;
            }

            // bug fix for double dialog box freezing in windows
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
                Title = "Choose file to export to",
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
                var exportFileName = Path.Combine(folder, AssetNameUtils.GetAssetFileName(Workspace, asset, exportExt));
                filesToWrite.Add((asset, exportFileName));
            }
        }
        else if (SelectedItems.Count == 1)
        {
            var asset = SelectedItems.First();
            var exportFileName = AssetNameUtils.GetAssetFileName(Workspace, asset, string.Empty);

            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Choose file to export",
                FileTypeChoices = new FilePickerFileType[]
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

        foreach (var (asset, file) in filesToWrite)
        {
            using var fs = File.OpenWrite(file);
            var exporter = new AssetExport(fs);

            if (file.EndsWith(".json") || file.EndsWith(".txt"))
            {
                var baseField = Workspace.GetBaseField(asset);
                if (baseField == null)
                {
                    fs.Write(Encoding.UTF8.GetBytes("Asset failed to deserialize."));
                }
                else
                {
                    if (file.EndsWith(".json"))
                    {
                        exporter.DumpJsonAsset(baseField);
                    }
                    else if (file.EndsWith(".txt"))
                    {
                        exporter.DumpTextAsset(baseField);
                    }
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

        ShowPluginsContextMenu?.Invoke();
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

    private async Task OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        await Load([]);
    }
}

public class PluginItemInfo
{
    public string Name { get; }

    private IUavPluginOption? _option;
    private AssetDocumentViewModel _docViewModel;

    public PluginItemInfo(string name, IUavPluginOption? option, AssetDocumentViewModel docViewModel)
    {
        Name = name;
        _option = option;
        _docViewModel = docViewModel;
    }

    public async Task Execute(object selectedItems)
    {
        if (_option != null)
        {
            var workspace = _docViewModel.Workspace;
            var res = await _option.Execute(workspace, new UavPluginFunctions(), _option.Options, (List<AssetInst>)selectedItems);
            if (res)
            {
                _docViewModel.ResendSelectedAssetsSelected();
            }
        }
    }

    public override string ToString()
    {
        return Name;
    }
}