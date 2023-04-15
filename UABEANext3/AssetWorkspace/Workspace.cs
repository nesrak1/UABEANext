using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UABEANext3.AssetWorkspace
{
    public class Workspace
    {
        public AssetsManager Manager { get; } = new AssetsManager();

        public ObservableCollection<WorkspaceItem> RootItems { get; } = new();
        public Dictionary<string, WorkspaceItem> ItemLookup { get; } = new();
        public Dictionary<AssetID, AssetInst> LoadedAssets { get; } = new();

        private bool _setMonoTempGeneratorsYet;

        public Workspace()
        {
            if (File.Exists("classdata.tpk"))
                Manager.LoadClassPackage("classdata.tpk");
        }

        public WorkspaceItem LoadBundle(Stream stream, string name = "")
        {
            BundleFileInstance bunInst;
            if (stream is FileStream fs)
            {
                bunInst = new BundleFileInstance(fs);
            }
            else
            {
                bunInst = new BundleFileInstance(stream, name);
            }

            WorkspaceItem item = new WorkspaceItem(this, bunInst);
            RootItems.Add(item);
            ItemLookup[bunInst.name] = item;

            return item;
        }

        public WorkspaceItem LoadAssets(Stream stream, string name = "")
        {
            AssetsFileInstance fileInst;
            if (stream is FileStream fs)
            {
                fileInst = new AssetsFileInstance(fs);
            }
            else
            {
                fileInst = new AssetsFileInstance(stream, name);
            }

            if (Manager.ClassDatabase == null)
            {
                Manager.LoadClassDatabaseFromPackage(fileInst.file.Metadata.UnityVersion);
            }
            WorkspaceItem item = new WorkspaceItem(fileInst);
            RootItems.Add(item);
            ItemLookup[fileInst.name] = item;

            return item;
        }

        public WorkspaceItem LoadResource(Stream stream, string name = "")
        {
            if (name == "")
            {
                if (stream is FileStream fs)
                {
                    name = Path.GetFileName(fs.Name);
                }
            }

            WorkspaceItem item = new WorkspaceItem(name, stream, WorkspaceItemType.ResourceFile);
            RootItems.Add(item);
            ItemLookup[name] = item;

            return item;
        }

        public AssetTypeTemplateField GetTemplateField(AssetInst asset, bool skipMonoBehaviourFields = false)
        {
            AssetReadFlags readFlags = AssetReadFlags.None;
            if (skipMonoBehaviourFields)
                readFlags |= AssetReadFlags.SkipMonoBehaviourFields;

            return Manager.GetTemplateBaseField(asset.FileInstance, asset.FileReader, asset.FilePosition, asset.ClassId, asset.MonoId, readFlags);
        }

        public AssetInst? GetAssetInst(AssetsFileInstance fileInst, int fileId, long pathId, bool onlyInfo = true)
        {
            if (fileId != 0)
            {
                fileInst = fileInst.GetDependency(Manager, fileId - 1);
            }

            if (fileInst != null)
            {
                AssetID assetId = new AssetID(fileInst.path, pathId);
                if (LoadedAssets.TryGetValue(assetId, out AssetInst? asset))
                {
                    if (!onlyInfo && asset.BaseValueField == null)
                    {
                        //// only set mono temp generator when we open a MonoBehaviour
                        //if ((asset.ClassId == (int)AssetClassID.MonoBehaviour || asset.ClassId < 0) && !_setMonoTempGeneratorsYet && !fileInst.file.Metadata.TypeTreeEnabled)
                        //{
                        //    string dataDir = Extensions.GetAssetsFileDirectory(fileInst);
                        //    bool success = SetMonoTempGenerators(dataDir);
                        //    if (!success)
                        //    {
                        //        MonoTemplateLoadFailed?.Invoke(dataDir);
                        //    }
                        //}

                        AssetTypeTemplateField tempField = GetTemplateField(asset);
                        try
                        {
                            AssetTypeValueField baseField = tempField.MakeValue(asset.FileReader, asset.FilePosition);
                            asset.BaseValueField = baseField;
                        }
                        catch
                        {
                            asset = null;
                        }
                    }
                    return asset;
                }
            }
            return null;
        }

        public AssetInst? GetAssetInst(AssetsFileInstance fileInst, AssetTypeValueField pptrField, bool onlyInfo = true)
        {
            int fileId = pptrField["m_FileID"].AsInt;
            long pathId = pptrField["m_PathID"].AsLong;
            return GetAssetInst(fileInst, fileId, pathId, onlyInfo);
        }

        public AssetTypeValueField? GetBaseField(AssetInst asset)
        {
            if (asset.BaseValueField != null)
                return asset.BaseValueField;

            AssetInst? newAsset = GetAssetInst(asset.FileInstance, 0, asset.PathId, false);
            if (newAsset != null)
                return newAsset.BaseValueField;
            else
                return null;
        }

        public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, int fileId, long pathId)
        {
            AssetInst? newAsset = GetAssetInst(fileInst, fileId, pathId, false);
            if (newAsset != null)
                return newAsset.BaseValueField;
            else
                return null;
        }
    }
}
