using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.ObjectModel;
using UABEANext3.AssetWorkspace;
using UABEANext3.Views.Tools;

namespace UABEANext3.ViewModels.Tools
{
    public class SceneExplorerToolViewModel : Tool
    {
        const string TOOL_TITLE = "Scene Explorer";

        public delegate void SelectedSceneItemChangedEvent(AssetInst asset);
        public event SelectedSceneItemChangedEvent? SelectedSceneItemChanged;

        public Workspace Workspace { get; }
        public ObservableCollection<SceneExplorerItem> RootItems { get; }

        [Obsolete("This is a previewer-only constructor")]
        public SceneExplorerToolViewModel()
        {
            Workspace = new();
            RootItems = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        public SceneExplorerToolViewModel(Workspace workspace)
        {
            Workspace = workspace;
            RootItems = new();

            Id = TOOL_TITLE.Replace(" ", "");
            Title = TOOL_TITLE;
        }

        // slow!
        public void LoadHierarchy(AssetsFileInstance fileInst)
        {
            //return;
            foreach (var asset in fileInst.file.AssetInfos)
            {
                var assetInst = (AssetInst)asset;
                if (assetInst.Type == AssetClassID.Transform)
                {
                    var transformBf = Workspace.GetBaseField(assetInst);
                    if (transformBf == null)
                        continue;

                    var father = transformBf["m_Father"];
                    if (AssetPPtr.FromField(father).IsNull())
                    {
                        RootItems.Add(new SceneExplorerItem(Workspace, assetInst));
                    }
                }
            }
        }

        public void InvokeSelectedSceneItemChanged(AssetInst asset)
        {
            SelectedSceneItemChanged?.Invoke(asset);
        }
    }
}
