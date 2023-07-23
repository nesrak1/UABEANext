using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Dock.Model.Controls;
using Dock.Model.Core;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Web;
using UABEANext3.AssetWorkspace;
using UABEANext3.AssetWorkspace.WorkspaceJobs;
using UABEANext3.Util;
using UABEANext3.ViewModels.Documents;
using UABEANext3.ViewModels.Tools;
using UABEAvalonia;

namespace UABEANext3.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IFactory _factory;
        private IRootDock? _layout;
        private double _progressValue;
        private string _progressText = "Done.";

        private readonly ServiceContainer _sc;
        private readonly Workspace _workspace;

        private OutputToolViewModel _outputToolViewModel;

        public IRootDock? Layout
        {
            get => _layout;
            set => this.RaiseAndSetIfChanged(ref _layout, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => this.RaiseAndSetIfChanged(ref _progressValue, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => this.RaiseAndSetIfChanged(ref _progressText, value);
        }

        public bool UsesChrome { get; set; } = OperatingSystem.IsWindows();
        public ExtendClientAreaChromeHints ChromeHints => UsesChrome
            ? ExtendClientAreaChromeHints.PreferSystemChrome
            : ExtendClientAreaChromeHints.Default;

        public MainWindowViewModel(ServiceContainer sc)
        {
            _sc = sc;

            _workspace = new();

            _factory = new MainDockFactory(_sc, _workspace);

            Layout = _factory.CreateLayout();
            if (Layout is { })
            {
                _factory.InitLayout(Layout);
            }

            SetupEvents();
        }

        private void SetupEvents()
        {
            var wsExp = _factory.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer")!;
            var scExp = _factory.GetDockable<SceneExplorerToolViewModel>("SceneExplorer")!;

            _outputToolViewModel = _factory.GetDockable<OutputToolViewModel>("Output")!;

            wsExp.SelectedWorkspaceItemChanged += WsExp_SelectedWorkspaceItemChanged;
            scExp.SelectedSceneItemChanged += ScExp_SelectedSceneItemChanged;

            _workspace.JobManager.JobProgressMessageFired += JobManager_JobProgressMessageFired;
            _workspace.JobManager.ProgressChanged += JobManager_ProgressChanged;
            _workspace.JobManager.JobsRunning += JobManager_JobsRunning;
        }

        private void JobManager_JobProgressMessageFired(object? sender, string e)
        {
            _outputToolViewModel.DisplayText += e + "\n";
        }

        private void JobManager_ProgressChanged(object? sender, float value)
        {
            ProgressValue = value;
        }

        private void JobManager_JobsRunning(object? sender, bool value)
        {
            if (value)
            {
                ProgressText = "Loading...";
            }
            else
            {
                ProgressText = "Done.";
            }
        }

        private void WsExp_SelectedWorkspaceItemChanged(List<WorkspaceItem> workspaceItems)
        {
            AssetDocumentViewModel document;
            AssetsFileInstance mainFileInst;
            if (workspaceItems.Count == 1)
            {
                var workspaceItem = workspaceItems[0];

                if (workspaceItem.ObjectType != WorkspaceItemType.AssetsFile)
                    return;

                mainFileInst = (AssetsFileInstance)workspaceItem.Object;

                document = new AssetDocumentViewModel(_sc, _workspace)
                {
                    Title = mainFileInst.name,
                    Id = mainFileInst.name
                };

                document.Load(new List<AssetsFileInstance>() { mainFileInst });
            }
            else
            {
                var assetsFileItems = workspaceItems
                    .Where(i => i.ObjectType == WorkspaceItemType.AssetsFile)
                    .Select(i => (AssetsFileInstance?)i.Object)
                    .Where(i => i != null).ToList()!;

                if (assetsFileItems.Count == 0)
                {
                    return;
                }

                mainFileInst = assetsFileItems[0]!;

                document = new AssetDocumentViewModel(_sc, _workspace)
                {
                    Title = $"{assetsFileItems.Count} files",
                    Id = (assetsFileItems.GetHashCode() * assetsFileItems.Count.GetHashCode()).ToString(), // todo random id
                };

                document.Load(assetsFileItems!);
            }

            document.AssetOpened += Document_AssetOpened;

            var files = _factory?.GetDockable<IDocumentDock>("Files");
            if (Layout is not null && files is not null)
            {
                if (files.ActiveDockable != null)
                {
                    var oldDockable = files.ActiveDockable;
                    _factory?.AddDockable(files, document);
                    _factory?.SetActiveDockable(document);
                    _factory?.SetFocusedDockable(files, document);
                    _factory?.SwapDockable(files, oldDockable, document);
                    _factory?.CloseDockable(oldDockable);
                }
                else
                {
                    _factory?.AddDockable(files, document);
                    _factory?.SetActiveDockable(document);
                    _factory?.SetFocusedDockable(files, document);
                }
            }

            var scene = _factory?.GetDockable<SceneExplorerToolViewModel>("SceneExplorer");
            if (Layout is not null && scene is not null)
            {
                scene.LoadHierarchy(mainFileInst);
            }
        }

        private void ScExp_SelectedSceneItemChanged(AssetInst asset)
        {
            if (asset.Type == AssetClassID.GameObject)
            {
                var gameObjectBf = _workspace.GetBaseField(asset);
                if (gameObjectBf == null)
                {
                    // todo: error msg
                    return;
                }
                SetInspectorItem(asset, true);
                var components = gameObjectBf["m_Component.Array"];
                foreach (var data in components)
                {
                    var component = data[data.Children.Count - 1];
                    AssetInst? componentInst = _workspace.GetAssetInst(asset.FileInstance, component);
                    if (componentInst == null)
                    {
                        // todo: error msg
                        continue;
                    }
                    SetInspectorItem(componentInst, false);
                }
            }
        }

        private void Document_AssetOpened(AssetInst asset)
        {
            SetInspectorItem(asset, true);
        }

        public void SetInspectorItem(AssetInst asset, bool reset)
        {
            var inspector = _factory?.GetDockable<InspectorToolViewModel>("Inspector");
            if (reset)
            {
                inspector.ActiveAssets = new() { asset };
            }
            else
            {
                inspector.ActiveAssets.Add(asset);
            }

            if (asset.Type == AssetClassID.Texture2D)
            {
                var previewer = _factory?.GetDockable<PreviewerToolViewModel>("Previewer");

                var textureEditBf = GetByteArrayTexture(_workspace, asset);
                var texture = TextureFile.ReadTextureFile(textureEditBf);
                var textureRgba = texture.GetTextureData(asset.FileInstance);

                if (textureRgba == null)
                    return;

                for (int i = 0; i < textureRgba.Length; i += 4)
                {
                    byte temp = textureRgba[i];
                    textureRgba[i] = textureRgba[i + 2];
                    textureRgba[i + 2] = temp;
                }

                previewer.SetImage(textureRgba, texture.m_Width, texture.m_Height);
            }
            else if (asset.Type == AssetClassID.TextAsset)
            {
                var previewer = _factory?.GetDockable<PreviewerToolViewModel>("Previewer");

                var textAssetBf = _workspace.GetBaseField(asset);
                if (textAssetBf == null)
                    return;

                byte[] data = textAssetBf["m_Script"].AsByteArray;
                previewer.SetText(data);
            }
        }

        public static AssetTypeValueField? GetByteArrayTexture(Workspace workspace, AssetInst tex)
        {
            AssetTypeTemplateField textureTemp = workspace.GetTemplateField(tex.FileInstance, tex);
            AssetTypeTemplateField? image_data = textureTemp.Children.FirstOrDefault(f => f.Name == "image data");
            if (image_data == null)
                return null;

            image_data.ValueType = AssetValueType.ByteArray;

            AssetTypeTemplateField? m_PlatformBlob = textureTemp.Children.FirstOrDefault(f => f.Name == "m_PlatformBlob");
            if (m_PlatformBlob != null)
            {
                AssetTypeTemplateField m_PlatformBlob_Array = m_PlatformBlob.Children[0];
                m_PlatformBlob_Array.ValueType = AssetValueType.ByteArray;
            }

            AssetTypeValueField baseField = textureTemp.MakeValue(tex.FileInstance.file.Reader, tex.AbsoluteByteStart);
            return baseField;
        }

        public async void FileOpen_Menu()
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
                    new FilePickerFileType("All files (*.*)") { Patterns = new[] {"*.*"} }
                },
                AllowMultiple = true
            });

            // todo: FileDialogUtils
            var jobs = result.Select(file => new OpenFilesWorkspaceJob(_workspace, HttpUtility.UrlDecode(file.Path.AbsolutePath))).Cast<IWorkspaceJob>().ToList();
            if (result != null)
            {
                await _workspace.JobManager.ProcessJobs(jobs);
            }
        }

        public async void FileSaveAllAs_Menu()
        {
            var storageProvider = StorageService.GetStorageProvider();
            if (storageProvider is null)
            {
                return;
            }

            var unsavedAssetsFiles = _workspace.UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.AssetsFile);
            foreach (var unsavedAssetsFile in unsavedAssetsFiles)
            {
                // skip assets files in bundles
                if (unsavedAssetsFile.Parent != null)
                    continue;

                var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save file",
                    FileTypeChoices = new FilePickerFileType[]
                    {
                        new FilePickerFileType("All files (*.*)") { Patterns = new[] {"*.*"} }
                    },
                    SuggestedFileName = unsavedAssetsFile.Name
                });

                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    var fileInst = (AssetsFileInstance)unsavedAssetsFile.Object!;
                    fileInst.file.Write(new AssetsFileWriter(stream));
                }
            }

            var unsavedBundleFiles = _workspace.UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.BundleFile);
            foreach (var unsavedBundleFile in unsavedBundleFiles)
            {
                // todo dupe
                var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save file",
                    FileTypeChoices = new FilePickerFileType[]
                    {
                        new FilePickerFileType("All files (*.*)") { Patterns = new[] {"*.*"} }
                    },
                    SuggestedFileName = unsavedBundleFile.Name
                });
                // /////////

                if (result != null)
                {
                    using var stream = await result.OpenWriteAsync();
                    var bunInst = (BundleFileInstance)unsavedBundleFile.Object!;

                    // files that are both unsaved and part of this bundle
                    var childrenFiles = unsavedBundleFile.Children
                        .Intersect(_workspace.UnsavedItems)
                        .ToDictionary(f => f.Name);

                    // sync up directory infos
                    var infos = bunInst.file.BlockAndDirInfo.DirectoryInfos;
                    foreach (var info in infos)
                    {
                        if (childrenFiles.TryGetValue(info.Name, out var unsavedAssetsFile))
                        {
                            if (unsavedAssetsFile.Object is AssetsFileInstance fileInst)
                            {
                                info.SetNewData(fileInst.file);
                            }
                            else if (unsavedAssetsFile.Object is AssetBundleDirectoryInfo matchingInfo && info == matchingInfo)
                            {
                                // do nothing, already handled by replacer
                            }
                            else
                            {
                                // shouldn't happen
                                info.Replacer = null;
                            }
                        }
                        else
                        {
                            // remove replacer (if there was one ever set)
                            info.Replacer = null;
                        }
                    }

                    bunInst.file.Write(new AssetsFileWriter(stream));
                }
            }

            var unsavedResourceFiles = _workspace.UnsavedItems.Where(i => i.ObjectType == WorkspaceItemType.ResourceFile);
            foreach (var unsavedResourceFile in unsavedResourceFiles)
            {
                // todo dupe
                var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save file",
                    FileTypeChoices = new FilePickerFileType[]
                    {
                        new FilePickerFileType("All files (*.*)") { Patterns = new[] {"*.*"} }
                    },
                    SuggestedFileName = unsavedResourceFile.Name
                });
                // /////////

                if (result != null)
                {
                    using var stream = await result!.OpenWriteAsync();
                    var dirInfo = (AssetBundleDirectoryInfo)unsavedResourceFile.Object!;
                    if (dirInfo.IsReplacerPreviewable)
                    {
                        dirInfo.Replacer.GetPreviewStream().CopyTo(stream);
                    }
                    else if (unsavedResourceFile.Parent != null)
                    {
                        var parentBundle = (BundleFileInstance)unsavedResourceFile.Parent.Object!;
                        var reader = parentBundle.file.DataReader;
                        reader.Position = dirInfo.Offset;
                        reader.BaseStream.CopyToCompat(stream, dirInfo.DecompressedSize);
                    }
                    else
                    {
                        // we can't do anything, not enough information
                    }
                }
            }
        }

        public async void FileXrefs_Menu()
        {
            var scanner = new SanicPPtrScanner(_workspace);

            var assetsFiles = _workspace.RootItems
                .Where(item => item.ObjectType == WorkspaceItemType.AssetsFile && item.Object != null)
                .Select(item => (AssetsFileInstance)item.Object!).ToList();

            var jobs = new List<IWorkspaceJob>() { new XRefsJob(scanner, assetsFiles) };

            await _workspace.JobManager.ProcessJobs(jobs);

            using var writer = new AssetsFileWriter($"scans/{Path.GetFileName(assetsFiles[0].name)}.uxrs");
            scanner.Save(writer);
        }
    }
}
