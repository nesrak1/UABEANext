using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UABEANext3.AssetWorkspace;
using UABEANext3.Logic;
using UABEANext3.Util;
using UABEANext3.ViewModels.Dialogs;
using static UABEANext3.ViewModels.Dialogs.BatchImportViewModel;

namespace UABEANext3.ViewModels.Documents
{
    public class AssetDocumentViewModel : Document
    {
        public ReadOnlyObservableCollection<AssetInst> Items { get; set; }

        public ServiceContainer Container { get; }
        private Workspace Workspace { get; }

        public delegate void AssetOpenedEvent(AssetInst assetInst);
        public event AssetOpenedEvent? AssetOpened;

        public List<AssetInst> SelectedItems { get; set; }
        public List<AssetsFileInstance> FileInsts { get; set; }

        private IDisposable? _disposableLastList;

        public ICommand EditDataCommand { get; }
        public Interaction<EditDataViewModel, byte[]?> ShowEditData { get; }
        public Interaction<BatchImportViewModel, List<ImportBatchInfo>> ShowBatchImport { get; }
        public Interaction<SelectDumpViewModel, SelectedDumpType?> ShowSelectDump { get; }

        // preview
        [Obsolete("This is a previewer-only constructor")]
        public AssetDocumentViewModel()
        {
            Container = new();
            Workspace = new();
            Items = new(new ObservableCollection<AssetInst>());

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            ShowBatchImport = new Interaction<BatchImportViewModel, List<ImportBatchInfo>>();
            ShowSelectDump = new Interaction<SelectDumpViewModel, SelectedDumpType?>();
            EditDataCommand = ReactiveCommand.CreateFromTask<AssetInst>(EditDataCommandMethod);
            SelectedItems = new List<AssetInst>();
            FileInsts = new List<AssetsFileInstance>();
        }
        // ///////

        public AssetDocumentViewModel(ServiceContainer sc, Workspace workspace)
        {
            Container = sc;
            Workspace = workspace;
            Items = new(new ObservableCollection<AssetInst>());

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            ShowBatchImport = new Interaction<BatchImportViewModel, List<ImportBatchInfo>>();
            ShowSelectDump = new Interaction<SelectDumpViewModel, SelectedDumpType?>();
            EditDataCommand = ReactiveCommand.CreateFromTask<AssetInst>(EditDataCommandMethod);
            SelectedItems = new List<AssetInst>();
            FileInsts = new List<AssetsFileInstance>();
        }

        private async Task EditDataCommandMethod(AssetInst asset)
        {
            var baseField = Workspace.GetBaseField(asset);
            if (baseField == null)
            {
                return;
            }

            var data = await ShowEditData.Handle(new EditDataViewModel(baseField));
            if (data == null)
            {
                return;
            }

            UpdateAssetDataAndRow(asset, data);

            var workspaceItem = Workspace.ItemLookup[asset.FileInstance.name];
            Workspace.Dirty(workspaceItem);
        }

        private void UpdateAssetDataAndRow(AssetInst asset, byte[] data)
        {
            asset.SetNewData(data);
            asset.BaseValueField = null; // clear basefield cache
            AssetNameUtils.GetDisplayNameFast(Workspace, asset, true, out string assetName, out string _);
            asset.DisplayName = assetName;
            asset.Update(nameof(asset.DisplayName));
            asset.Update(nameof(asset.ByteSizeModified));
            asset.Update(nameof(asset.ModifiedString));
        }

        public void Load(List<AssetsFileInstance> fileInsts)
        {
            if (Workspace == null)
                return;

            _disposableLastList?.Dispose();

            var sourceList = new SourceList<RangeObservableCollection<AssetFileInfo>>();
            foreach (var fileInst in fileInsts)
            {
                if (fileInst.file.AssetInfos is not RangeObservableCollection<AssetFileInfo>)
                {
                    var assetInsts = new RangeObservableCollection<AssetFileInfo>();
                    var tmp = new List<AssetFileInfo>();
                    foreach (var info in fileInst.file.AssetInfos)
                    {
                        AssetInst asset = new AssetInst(fileInst, info);
                        AssetNameUtils.GetDisplayNameFast(Workspace, asset, true, out string assetName, out string _);
                        asset.DisplayName = assetName;
                        tmp.Add(asset);
                    }
                    assetInsts.AddRange(tmp);
                    fileInst.file.Metadata.AssetInfos = assetInsts;
                }

                var infosObsCol = (RangeObservableCollection<AssetFileInfo>)fileInst.file.Metadata.AssetInfos;
                sourceList.Add(infosObsCol);
            }

            var observableList = sourceList
                .Connect()
                .MergeMany(e => e.ToObservableChangeSet())
                .Transform(a => (AssetInst)a);
            _disposableLastList = observableList.Bind(out var items).Subscribe();
            Items = items;
            FileInsts = fileInsts;

            GC.Collect();
        }

        public void Import()
        {
            if (SelectedItems.Count > 1)
            {
                ImportBatch(SelectedItems.ToList());
            }
            else
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

            var folders = FileDialogUtils.GetOpenFolderDialogFiles(result);
            if (folders == null || folders.Length != 1)
                return;

            List<string> exts = new List<string>()
            {
                "json", "txt", "dat"
            };

            var fileNamesToDirty = new HashSet<string>();
            var batchInfos = await ShowBatchImport.Handle(new BatchImportViewModel(Workspace, assets, folders[0], exts));
            foreach (ImportBatchInfo batchInfo in batchInfos)
            {
                var selectedFilePath = batchInfo.ImportFile;
                if (selectedFilePath == null)
                    continue;

                var selectedAsset = batchInfo.Asset;
                var selectedInst = selectedAsset.FileInstance;

                using (FileStream fs = File.OpenRead(selectedFilePath))
                using (StreamReader sr = new StreamReader(fs))
                {
                    var importer = new AssetImportExport();

                    byte[]? data;
                    string? exceptionMessage;

                    if (selectedFilePath.EndsWith(".json"))
                    {
                        var tempField = Workspace.GetTemplateField(selectedAsset);
                        data = importer.ImportJsonAsset(tempField, sr, out exceptionMessage);
                    }
                    else if (selectedFilePath.EndsWith(".txt"))
                    {
                        data = importer.ImportTextAsset(sr, out exceptionMessage);
                    }
                    else
                    {
                        exceptionMessage = string.Empty;
                        data = importer.ImportRawAsset(fs);
                    }

                    if (data == null)
                    {
                        await MessageBoxUtil.ShowDialog("Parse error", "Something went wrong when reading the dump file:\n" + exceptionMessage);
                        goto dirtyFiles;
                    }

                    UpdateAssetDataAndRow(selectedAsset, data);
                    fileNamesToDirty.Add(selectedAsset.FileInstance.name);
                }
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
                    new FilePickerFileType("UABEA json dump (*.json)") { Patterns = new[] {"*.json"} },
                    new FilePickerFileType("UABE txt dump (*.txt)") { Patterns = new[] {"*.txt"} },
                    new FilePickerFileType("Raw dump (*.dat)") { Patterns = new[] {"*.dat"} },
                    new FilePickerFileType("Raw dump (*.*)") { Patterns = new[] {"*.*"} },
                },
            });

            var files = FileDialogUtils.GetOpenFileDialogFiles(result);
            if (files == null || files.Length == 0)
                return;

            var file = files[0];

            var importer = new AssetImportExport();

            byte[]? data = null;
            string? exception;

            if (file.EndsWith(".json") || file.EndsWith(".txt"))
            {
                using var reader = new StreamReader(File.OpenRead(file));
                if (file.EndsWith(".json"))
                {
                    var baseField = Workspace.GetTemplateField(asset);
                    data = importer.ImportJsonAsset(baseField, reader, out exception);
                }
                else if (file.EndsWith(".txt"))
                {
                    data = importer.ImportTextAsset(reader, out exception);
                }
            }
            else //if (file.EndsWith(".dat"))
            {
                using var stream = File.OpenRead(file);
                data = importer.ImportRawAsset(stream);
            }

            if (data != null)
            {
                UpdateAssetDataAndRow(asset, data);
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
            var selectedAssets = SelectedItems.Cast<AssetInst>();
            if (selectedAssets.Count() > 1)
            {
                var exportType = await ShowSelectDump.Handle(new SelectDumpViewModel(true));
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

                var folders = FileDialogUtils.GetOpenFolderDialogFiles(result);
                if (folders.Length == 0)
                {
                    return;
                }

                var folder = folders[0];
                foreach (var asset in selectedAssets)
                {
                    var exportFileName = Path.Combine(folder, GetAssetFileName(asset, exportExt));
                    filesToWrite.Add((asset, exportFileName));
                }
            }
            else
            {
                var asset = selectedAssets.First();
                var exportFileName = GetAssetFileName(asset, string.Empty);

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

            var impExpTool = new AssetImportExport();
            foreach (var (asset, file) in filesToWrite)
            {
                if (file.EndsWith(".json") || file.EndsWith(".txt"))
                {
                    using var writer = new StreamWriter(File.OpenWrite(file));
                    var baseField = Workspace.GetBaseField(asset);
                    if (baseField == null)
                    {
                        writer.Write("Asset failed to deserialize.");
                    }
                    else
                    {
                        if (file.EndsWith(".json"))
                        {
                            impExpTool.DumpJsonAsset(writer, baseField);
                        }
                        else if (file.EndsWith(".txt"))
                        {
                            impExpTool.DumpTextAsset(writer, baseField);
                        }
                    }
                }
                else if (file.EndsWith(".dat"))
                {
                    var stream = File.OpenWrite(file);
                    if (asset.IsReplacerPreviewable)
                    {
                        var preview = asset.Replacer.GetPreviewStream();
                        var previewReader = new AssetsFileReader(preview);
                        impExpTool.DumpRawAsset(stream, previewReader, 0, (uint)preview.Length);
                    }
                    else
                    {
                        impExpTool.DumpRawAsset(stream, asset.FileReader, asset.AbsoluteByteStart, asset.ByteSize);
                    }
                }
            }
        }

        private string GetAssetFileName(AssetInst asset, string ext)
        {
            AssetNameUtils.GetDisplayNameFast(Workspace, asset, false, out string assetName, out string _);
            assetName = PathUtils.ReplaceInvalidPathChars(assetName);
            return $"{assetName}-{Path.GetFileName(asset.FileInstance.path)}-{asset.PathId}{ext}";
        }

        public void EditData()
        {
            var selectedAssetInsts = SelectedItems.Cast<AssetInst>();
            var asset = selectedAssetInsts.First();

            EditDataCommand.Execute(asset);
        }

        public void InvokeAssetOpened(List<AssetInst> assets)
        {
            if (assets.Count > 0)
                AssetOpened?.Invoke(assets.Last());

            SelectedItems = assets;
        }
    }
}
