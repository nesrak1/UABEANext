using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public List<SceneExplorerItem> RootItems { get; }

        // preview only
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

        public void LoadHierarchy(AssetsFileInstance fileInst)
        {
            foreach (var asset in Workspace.LoadedAssets.Values)
            {
                if (asset.FileInstance == fileInst && asset.Type == AssetClassID.Transform)
                {
                    var transformBf = Workspace.GetBaseField(asset);
                    if (transformBf == null)
                        continue;

                    var father = transformBf["m_Father"];
                    if (AssetPPtr.FromField(father).IsNull())
                    {
                        RootItems.Add(new SceneExplorerItem(Workspace, asset));
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
