using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Dock.Model.Controls;
using Dock.Model.Core;
using DynamicData;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UABEANext3.AssetWorkspace;
using UABEANext3.AssetWorkspace.WorkspaceJobs;
using UABEANext3.Util;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.ViewModels.Documents;
using UABEANext3.ViewModels.Tools;
using UABEAvalonia;

namespace UABEANext3.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IFactory _factory;

        private readonly ServiceContainer _sc;
        private readonly Workspace _workspace;

        private OutputToolViewModel _outputToolViewModel;

        [Reactive]
        public IRootDock? Layout { get; set; }
        [Reactive]
        public double ProgressValue { get; set; }
        [Reactive]
        public string ProgressText { get; set; }
        
        public Interaction<AssetInfoViewModel, AssetInfoViewModel?> ShowAssetInfo { get; }

        public ICommand GetAssetInfoCommand { get; }

        public bool UsesChrome => OperatingSystem.IsWindows();
        public ExtendClientAreaChromeHints ChromeHints => UsesChrome
            ? ExtendClientAreaChromeHints.PreferSystemChrome
            : ExtendClientAreaChromeHints.Default;

        [Obsolete("This is a previewer-only constructor")]
        public MainWindowViewModel()
        {
            _workspace = new();
            _factory = new MainDockFactory(_sc, _workspace);

            Layout = _factory.CreateLayout();
            if (Layout is { })
            {
                _factory.InitLayout(Layout);
            }

        }

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
            
            ShowAssetInfo = new Interaction<AssetInfoViewModel, AssetInfoViewModel?>();
            
            GetAssetInfoCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var store = new AssetInfoViewModel();
                var result = await ShowAssetInfo.Handle(store);
            });
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

            var files = _factory?.GetDockable<IDocumentDock>("Files");
            if (Layout is not null && files is not null)
            {
                files.ObservableForProperty(x => x.ActiveDockable).Subscribe(x =>
                {
                    if (x.Value is AssetDocumentViewModel assetDocument)
                    {
                        var files = assetDocument.FileInsts;

                        var scene = _factory?.GetDockable<SceneExplorerToolViewModel>("SceneExplorer");
                        if (Layout is not null && scene is not null)
                        {
                            for (var i = 0; i < files.Count; i++)
                            {
                                var file = files[i];
                                scene.LoadHierarchy(file, i == 0);
                            }
                        }
                    }
                });
            }
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
                    _factory?.SwapDockable(files, oldDockable, document);
                    _factory?.CloseDockable(oldDockable);
                    _factory?.SetActiveDockable(document);
                    _factory?.SetFocusedDockable(files, document);
                }
                else
                {
                    _factory?.AddDockable(files, document);
                    _factory?.SetActiveDockable(document);
                    _factory?.SetFocusedDockable(files, document);
                }
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
            if (inspector != null)
            {
                if (reset)
                {
                    inspector.ActiveAssets = new() { asset };
                }
                else
                {
                    inspector.ActiveAssets.Add(asset);
                }
            }

            try
            {
                if (asset.Type == AssetClassID.Texture2D)
                {
                    var previewer = _factory?.GetDockable<PreviewerToolViewModel>("Previewer");
                    if (previewer != null)
                    {
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
                }
                else if (asset.Type == AssetClassID.TextAsset)
                {
                    var previewer = _factory?.GetDockable<PreviewerToolViewModel>("Previewer");
                    if (previewer != null)
                    {
                        var textAssetBf = _workspace.GetBaseField(asset);
                        if (textAssetBf == null)
                            return;

                        byte[] data = textAssetBf["m_Script"].AsByteArray;
                        previewer.SetText(data);
                    }
                }
                else if (asset.Type == AssetClassID.Mesh)
                {
                    var previewer = _factory?.GetDockable<PreviewerToolViewModel>("Previewer");
                    if (previewer != null)
                    {
                        previewer.SetMesh(asset);
                    }
                }
            }
            catch { }
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

        public async Task OpenFiles(IEnumerable<string> files)
        {
            var jobs = files.Select(file => new OpenFilesWorkspaceJob(_workspace, file)).Cast<IWorkspaceJob>().ToList();
            if (jobs.Count > 0)
            {
                await _workspace.JobManager.ProcessJobs(jobs);
            }
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

            var fileNames = FileDialogUtils.GetOpenFileDialogFiles(result);
            await OpenFiles(fileNames);
        }

        public async void FileSaveAs_Menu()
        {
            var explorer = _factory?.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");
            if (explorer == null)
                return;

            var items = explorer.SelectedItems.Cast<WorkspaceItem>();
            foreach (var item in items)
            {
                await _workspace.SaveAs(item);
            }
        }

        public async void FileSaveAllAs_Menu()
        {
            await _workspace.SaveAllAs();
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

        public void FileCloseAll_Menu()
        {
            _workspace.CloseAll();
            var files = _factory?.GetDockable<IDocumentDock>("Files");
            if (files is { } && files.VisibleDockables != null && files.VisibleDockables.Count > 0)
            {
                // lol you have to pass in a child
                _factory?.CloseAllDockables(files.VisibleDockables[0]);
            }
        }

        public void ViewDuplicateTab_Menu()
        {
            var files = _factory?.GetDockable<IDocumentDock>("Files");
            if (Layout is not null && files is not null)
            {
                if (files.ActiveDockable != null)
                {
                    var oldDockable = files.ActiveDockable;
                    _factory?.AddDockable(files, oldDockable);
                }
            }
        }

        public async void ToolsGeneralInfo_Menu()
        {
            var explorer = _factory?.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");
            if (explorer == null)
                return;

            if (explorer.SelectedItems.Cast<WorkspaceItem>().LastOrDefault(x => x.ObjectType == WorkspaceItemType.BundleFile) is not { } item)
                return;
            
            var a = await ShowAssetInfo.Handle(new AssetInfoViewModel(item));
        }
    }
}
