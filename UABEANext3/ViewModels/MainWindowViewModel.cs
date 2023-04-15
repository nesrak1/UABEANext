using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Collections;
using Avalonia.Platform.Storage;
using Dock.Model.Controls;
using Dock.Model.Core;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UABEANext3.AssetWorkspace;
using UABEANext3.Util;
using UABEANext3.ViewModels.Documents;
using UABEANext3.ViewModels.Tools;
using UABEANext3.Views.Documents;

namespace UABEANext3.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IFactory? _factory;
        private IRootDock? _layout;

        private Workspace _workspace;

        public IRootDock? Layout
        {
            get => _layout;
            set => this.RaiseAndSetIfChanged(ref _layout, value);
        }

        public MainWindowViewModel()
        {
            _workspace = new();

            _factory = new MainDockFactory(_workspace);

            Layout = _factory?.CreateLayout();
            if (Layout is { })
            {
                _factory?.InitLayout(Layout);
            }

            SetupEvents();
        }

        private void SetupEvents()
        {
            var wsExp = _factory?.GetDockable<WorkspaceExplorerToolViewModel>("WorkspaceExplorer");
            var scExp = _factory?.GetDockable<SceneExplorerToolViewModel>("SceneExplorer");

            wsExp.SelectedWorkspaceItemChanged += WsExp_SelectedWorkspaceItemChanged;
            scExp.SelectedSceneItemChanged += ScExp_SelectedSceneItemChanged;
        }

        private void WsExp_SelectedWorkspaceItemChanged(WorkspaceItem workspaceItem)
        {
            if (workspaceItem.ObjectType != WorkspaceItemType.AssetsFile)
                return;

            var fileInst = (AssetsFileInstance)workspaceItem.Object;

            var document = new AssetDocumentViewModel(_workspace)
            {
                Title = fileInst.name,
                Id = fileInst.name
            };

            document.Load(fileInst);

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
                scene.LoadHierarchy(fileInst);
            }
        }

        private void ScExp_SelectedSceneItemChanged(AssetInst asset)
        {
            if (asset.Type == AssetClassID.GameObject)
            {
                var gameObjectBf = _workspace.GetBaseField(asset);
                var components = gameObjectBf["m_Component.Array"];
                bool firstTime = true;
                foreach (var data in components)
                {
                    var component = data[data.Children.Count - 1];
                    AssetInst componentInst = _workspace.GetAssetInst(asset.FileInstance, component, false);
                    SetInspectorItem(componentInst, firstTime);
                    firstTime = false;
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
                var tmp = inspector.ActiveAssets;
                inspector.ActiveAssets = new AvaloniaList<AssetInst>();
                inspector.ActiveAssets = tmp;
            }

            if ((AssetClassID)asset.ClassId == AssetClassID.Texture2D)
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
        }

        public static AssetTypeValueField? GetByteArrayTexture(Workspace workspace, AssetInst tex)
        {
            AssetTypeTemplateField textureTemp = workspace.GetTemplateField(tex);
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

            AssetTypeValueField baseField = textureTemp.MakeValue(tex.FileReader, tex.FilePosition);
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

            foreach (var file in result)
            {
                var fileStream = await file.OpenReadAsync();
                var detectedType = AssetBundleDetector.DetectFileType(new AssetsFileReader(fileStream), 0);
                if (detectedType == DetectedFileType.BundleFile)
                {
                    fileStream.Position = 0;
                    _workspace.LoadBundle(fileStream);
                }
                else if (detectedType == DetectedFileType.AssetsFile)
                {
                    fileStream.Position = 0;
                    _workspace.LoadAssets(fileStream);
                }
                else if (file.Name.EndsWith(".resS") || file.Name.EndsWith(".resource"))
                {
                    _workspace.LoadResource(fileStream);
                }
            }
        }
    }
}
