using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;

namespace UABEANext3.Views.Tools
{
    public class SceneExplorerItem
    {
        // todo events for things like name changes
        public AssetInst Asset { get; set; }
        public List<SceneExplorerItem> Children { get; }
        public string Name { get; set; }

        public SceneExplorerItem(Workspace workspace, AssetInst transformInst)
        {
            Children = new();
            Name = "???";

            AssetTypeValueField transformBf = workspace.GetBaseField(transformInst);

            AssetTypeValueField gameObjectRef = transformBf["m_GameObject"];
            AssetInst gameObjectInst = workspace.GetAssetInst(transformInst.FileInstance, gameObjectRef, false);

            Asset = gameObjectInst;

            if (gameObjectInst == null)
                return;

            AssetTypeValueField? gameObjectBf = workspace.GetBaseField(gameObjectInst);
            Name = gameObjectBf["m_Name"].AsString;

            AssetTypeValueField children = transformBf["m_Children"]["Array"];
            foreach (AssetTypeValueField child in children)
            {
                AssetInst childTransformInst = workspace.GetAssetInst(transformInst.FileInstance, child, false);
                var childSeItem = new SceneExplorerItem(workspace, childTransformInst);
                Children.Add(childSeItem);
            }
        }
    }
}
