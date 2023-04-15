using AssetsTools.NET.Extra;
using AssetsTools.NET;
using Avalonia.Controls;
using Avalonia.Input;
using Dock.Model.ReactiveUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;
using UABEANext3.Views;
using Avalonia.Collections;
using UABEANext2;

namespace UABEANext3.ViewModels.Documents
{
    internal class AssetDocumentViewModel : Document
    {
        public AvaloniaList<AssetInst> Items { get; } = new();

        Workspace Workspace { get; }

        public delegate void AssetOpenedEvent(AssetInst assetInst);
        public event AssetOpenedEvent? AssetOpened;

        // preview only
        public AssetDocumentViewModel()
        {
            Workspace = new();
        }

        public AssetDocumentViewModel(Workspace workspace)
        {
            Workspace = workspace;
        }

        public void Load(AssetsFileInstance fileInst)
        {
            if (Workspace == null)
                return;

            foreach (AssetFileInfo info in fileInst.file.AssetInfos)
            {
                AssetInst asset = new AssetInst(info, fileInst);
                Utils.GetUABENameFast(Workspace, asset, true, out string assetName, out string _);
                asset.DisplayName = assetName;
                Workspace.LoadedAssets[asset.AssetId] = asset;
                Items.Add(asset);
            }
        }

        public void InvokeAssetOpened(AssetInst asset)
        {
            AssetOpened?.Invoke(asset);
        }
    }
}
