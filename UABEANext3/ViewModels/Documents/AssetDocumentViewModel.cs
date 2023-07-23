using AssetsTools.NET.Extra;
using AssetsTools.NET;
using Avalonia.Platform.Storage;
using Dock.Model.ReactiveUI.Controls;
using DynamicData.Binding;
using DynamicData;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive.Linq;
using System.Windows.Input;
using System;
using UABEANext3.AssetWorkspace;
using UABEANext3.Logic;
using UABEANext3.Util;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.Services;
using Autofac;
using UABEANext3.Views.Dialogs;
using System.ComponentModel.Design;

namespace UABEANext3.ViewModels.Documents
{
    public class AssetDocumentViewModel : Document
    {
        //public ObservableCollection<AssetFileInfo> Items { get; set; }
        public ReadOnlyObservableCollection<AssetInst> Items { get; set; }

        public ServiceContainer Container { get; }
        private Workspace Workspace { get; }

        public delegate void AssetOpenedEvent(AssetInst assetInst);
        public event AssetOpenedEvent? AssetOpened;

        private AssetFileInfo? _selectedItem;
        public AssetFileInfo? SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

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
        }
        // ///////

        public AssetDocumentViewModel(ServiceContainer sc, Workspace workspace)
        {
            Container = sc;
            Workspace = workspace;
            Items = new(new ObservableCollection<AssetInst>());

            ShowEditData = new Interaction<EditDataViewModel, byte[]?>();
            EditDataCommand = ReactiveCommand.CreateFromTask<AssetInst> (async (asset) =>
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

                asset.SetNewData(data);
                asset.BaseValueField = null;
                AssetNameUtils.GetDisplayNameFast(Workspace, asset, true, out string assetName, out string _);
                asset.DisplayName = assetName;
                asset.Update(nameof(asset.DisplayName));
                asset.Update(nameof(asset.ByteSizeModified));
                asset.Update(nameof(asset.ModifiedString));
            });
        }

        public void Load(List<AssetsFileInstance> fileInsts)
        {
            if (Workspace == null)
                return;

            _disposableLastList?.Dispose();

            //List<AssetInst> items = new List<AssetInst>();
            //foreach (var fileInst in fileInsts)
            //{
            //    IList<AssetFileInfo> infos = fileInst.file.AssetInfos;
            //    if (fileInst.file.AssetInfos is not RangeObservableCollection<AssetFileInfo>)
            //    {
            //        var tmp = new List<AssetFileInfo>();
            //        for (int i = 0; i < infos.Count; i++)
            //        {
            //            AssetInst asset = new AssetInst(fileInst, infos[i]);
            //            AssetNameUtils.GetDisplayNameFast(Workspace, asset, true, out string assetName, out string _);
            //            asset.DisplayName = assetName;
            //            infos[i] = asset;
            //        }
            //        var tmp2 = new RangeObservableCollection<AssetFileInfo>();
            //        tmp2.AddRange(fileInst.file.AssetInfos);
            //        fileInst.file.Metadata.AssetInfos = tmp2;
            //        fileInst.file.Metadata.AssetInfos = tmp;
            //    }
            //    items.AddRange(infos.Cast<AssetInst>());
            //}

            //GC.Collect();

            //Items = items;

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
            if (files == null)
                return;

            var fileNamesToDirty = new HashSet<string>();
            foreach (var file in files)
            {
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
                else if (file.EndsWith(".dat"))
                {
                    using var stream = File.OpenRead(file);
                    data = impExpTool.ImportRawAsset(stream);
                }

                if (data != null)
                {
                    asset.SetNewData(data);
                    asset.BaseValueField = null;
                    AssetNameUtils.GetDisplayNameFast(Workspace, asset, true, out string assetName, out string _);
                    asset.DisplayName = assetName;
                    asset.Update(nameof(asset.DisplayName));
                    asset.Update(nameof(asset.ByteSizeModified));
                    asset.Update(nameof(asset.ModifiedString));
                }

                fileNamesToDirty.Add(asset.FileInstance.name);
            }

            foreach (var fileName in fileNamesToDirty)
            {
                var file = Workspace.ItemLookup[fileName];
                Workspace.Dirty(file);
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

        //public async void EditData()
        //{
        //    if (SelectedItem == null || SelectedItem is not AssetInst asset)
        //    {
        //        return;
        //    }
        //
        //    var editData = new EditDataView(Workspace.GetBaseField(asset)!);
        //    var data = await editData.ShowDialog<byte[]?>(WindowUtils.GetMainWindow());
        //    if (data == null)
        //    {
        //        return;
        //    }
        //    
        //    asset.SetNewData(data);
        //    asset.BaseValueField = null;
        //    AssetNameUtils.GetDisplayNameFast(Workspace, asset, true, out string assetName, out string _);
        //    asset.DisplayName = assetName;
        //    asset.Update(nameof(asset.DisplayName));
        //    asset.Update(nameof(asset.ByteSizeModified));
        //    asset.Update(nameof(asset.ModifiedString));
        //}

        public void InvokeAssetOpened(AssetInst asset)
        {
            AssetOpened?.Invoke(asset);
        }
    }
}
