using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using Dock.Model.ReactiveUI.Controls;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.IO;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UABEANext3.AssetWorkspace;
using UABEANext3.Logic;
using UABEANext3.Util;
using UABEANext3.ViewModels.Dialogs;

namespace UABEANext3.ViewModels.Documents
{
    public class AssetDocumentViewModel : Document
    {
        public ReadOnlyObservableCollection<AssetInst> Items { get; set; }

        public ServiceContainer Container { get; }
        private Workspace Workspace { get; }

        public delegate void AssetOpenedEvent(AssetInst assetInst);
        public event AssetOpenedEvent? AssetOpened;

        [Reactive]
        public AssetFileInfo? SelectedItem { get; set; }

        private IDisposable? _disposableLastList;

        public ICommand EditDataCommand { get; }
        public Interaction<EditDataViewModel, byte[]?> ShowEditData { get; }

        // preview
        [Obsolete("This is a previewer-only constructor")]
        public AssetDocumentViewModel()
        {
            Container = new();
            Workspace = new();
            Items = new(new ObservableCollection<AssetInst>());

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            EditDataCommand = ReactiveCommand.CreateFromTask<AssetInst>(EditDataCommandMethod);
        }
        // ///////

        public AssetDocumentViewModel(ServiceContainer sc, Workspace workspace)
        {
            Container = sc;
            Workspace = workspace;
            Items = new(new ObservableCollection<AssetInst>());

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            EditDataCommand = ReactiveCommand.CreateFromTask<AssetInst>(EditDataCommandMethod);
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

            GC.Collect();
        }

        public async void Import()
        {
            if (SelectedItem == null || SelectedItem is not AssetInst asset)
            {
                return;
            }

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

            var fileNamesToDirty = new HashSet<string>();
            //foreach (var file in files) // todo: support multi file import
            {
                var file = files[0];

                byte[]? data = null;
                string? exception;
                var impExpTool = new AssetImportExport();
                if (file.EndsWith(".json") || file.EndsWith(".txt"))
                {
                    using var reader = new StreamReader(File.OpenRead(file));
                    if (file.EndsWith(".json"))
                    {
                        var baseField = Workspace.GetTemplateField(asset);
                        data = impExpTool.ImportJsonAsset(baseField, reader, out exception);
                    }
                    else if (file.EndsWith(".txt"))
                    {
                        data = impExpTool.ImportTextAsset(reader, out exception);
                    }
                }
                else //if (file.EndsWith(".dat"))
                {
                    using var stream = File.OpenRead(file);
                    data = impExpTool.ImportRawAsset(stream);
                }

                if (data != null)
                {
                    UpdateAssetDataAndRow(asset, data);
                }

                fileNamesToDirty.Add(asset.FileInstance.name);
            }

            foreach (var fileName in fileNamesToDirty)
            {
                var workspaceItem = Workspace.ItemLookup[fileName];
                Workspace.Dirty(workspaceItem);
            }
        }

        public async void Export()
        {
            if (SelectedItem == null || SelectedItem is not AssetInst asset)
            {
                return;
            }

            var storageProvider = StorageService.GetStorageProvider();
            if (storageProvider is null)
            {
                return;
            }

            var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Choose file to export",
                FileTypeChoices = new FilePickerFileType[]
                {
                    new FilePickerFileType("UABEA json dump (*.json)") { Patterns = new[] {"*.json"} },
                    new FilePickerFileType("UABE txt dump (*.txt)") { Patterns = new[] {"*.txt"} },
                    new FilePickerFileType("Raw dump (*.dat)") { Patterns = new[] {"*.dat"} }
                },
            });

            var file = FileDialogUtils.GetSaveFileDialogFile(result);
            if (file != null)
            {
                var impExpTool = new AssetImportExport();
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

        public void EditData()
        {
            if (SelectedItem == null || SelectedItem is not AssetInst asset)
            {
                return;
            }

            EditDataCommand.Execute(asset);
        }

        public void InvokeAssetOpened(AssetInst asset)
        {
            AssetOpened?.Invoke(asset);
        }
    }
}
