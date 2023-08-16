using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UABEANext3.Util;

namespace UABEANext3.AssetWorkspace
{
    public partial class Workspace
    {
        public AssetsManager Manager { get; } = new AssetsManager();
        public WorkspaceJobManager JobManager { get; } = new WorkspaceJobManager();

        public ObservableCollection<WorkspaceItem> RootItems { get; } = new();
        public Dictionary<string, WorkspaceItem> ItemLookup { get; } = new();

        // items modified and unsaved
        public HashSet<WorkspaceItem> UnsavedItems { get; } = new();
        // items modified and saved
        // we track this since the base AssetsFile is still reading from the old file
        public HashSet<WorkspaceItem> ModifiedItems { get; } = new();

        public delegate void MonoTemplateFailureEvent(string path);
        public event MonoTemplateFailureEvent? MonoTemplateLoadFailed;

        private bool _setMonoTempGeneratorsYet;

        public Workspace()
        {
            string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath))
                Manager.LoadClassPackage(classDataPath);

            Manager.UseRefTypeManagerCache = true;
            Manager.UseTemplateFieldCache = true;
            Manager.UseQuickLookup = true;
        }

        public WorkspaceItem LoadBundle(Stream stream, string name = "")
        {
            // todo: don't always unpack to memory lol
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
            AddRootItemThreadSafe(item, bunInst.name);

            return item;
        }

        public WorkspaceItem LoadAssets(Stream stream, string name = "")
        {
            AssetsFileInstance fileInst;
            if (stream is FileStream fs)
            {
                fileInst = Manager.LoadAssetsFile(fs);
            }
            else
            {
                fileInst = Manager.LoadAssetsFile(stream, name);
            }

            if (Manager.ClassDatabase == null)
            {
                Manager.LoadClassDatabaseFromPackage(fileInst.file.Metadata.UnityVersion);
            }
            WorkspaceItem item = new WorkspaceItem(fileInst);
            AddRootItemThreadSafe(item, fileInst.name);

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
            AddRootItemThreadSafe(item, name);

            return item;
        }

        private void AddRootItemThreadSafe(WorkspaceItem item, string itemName)
        {
            lock (RootItems)
            {
                if (Dispatcher.UIThread.CheckAccess())
                {
                    RootItems.Add(item);
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        RootItems.Add(item);
                    }, DispatcherPriority.Background);
                }
            }
            lock (ItemLookup)
            {
                ItemLookup[itemName] = item;
            }
        }

        // should be nullable
        public AssetTypeTemplateField GetTemplateField(AssetInst asset, bool skipMonoBehaviourFields = false)
        {
            AssetReadFlags readFlags = AssetReadFlags.None;
            if (skipMonoBehaviourFields && asset.Type == AssetClassID.MonoBehaviour)
            {
                readFlags |= AssetReadFlags.SkipMonoBehaviourFields | AssetReadFlags.ForceFromCldb;
            }

            return Manager.GetTemplateBaseField(asset.FileInstance, asset, readFlags);
        }

        public AssetTypeTemplateField GetTemplateField(AssetsFileInstance fileInst, AssetFileInfo info, bool skipMonoBehaviourFields = false)
        {
            AssetReadFlags readFlags = AssetReadFlags.None;
            if (skipMonoBehaviourFields && info.TypeId == (int)AssetClassID.MonoBehaviour)
            {
                readFlags |= AssetReadFlags.SkipMonoBehaviourFields | AssetReadFlags.ForceFromCldb;
            }

            return Manager.GetTemplateBaseField(fileInst, info, readFlags);
        }

        private void CheckAndSetMonoTempGenerators(AssetsFileInstance fileInst, AssetFileInfo info)
        {
            if ((info.TypeId == (int)AssetClassID.MonoBehaviour || info.TypeId < 0) && !_setMonoTempGeneratorsYet && !fileInst.file.Metadata.TypeTreeEnabled)
            {
                string dataDir = PathUtils.GetAssetsFileDirectory(fileInst);
                bool success = SetMonoTempGenerators(dataDir);
                if (!success)
                {
                    MonoTemplateLoadFailed?.Invoke(dataDir);
                }
            }
        }

        private bool SetMonoTempGenerators(string fileDir)
        {
            if (!_setMonoTempGeneratorsYet)
            {
                _setMonoTempGeneratorsYet = true;

                string managedDir = Path.Combine(fileDir, "Managed");
                if (Directory.Exists(managedDir))
                {
                    bool hasDll = Directory.GetFiles(managedDir, "*.dll").Any();
                    if (hasDll)
                    {
                        Manager.MonoTempGenerator = new MonoCecilTempGenerator(managedDir);
                        return true;
                    }
                }

                FindCpp2IlFilesResult il2cppFiles = FindCpp2IlFiles.Find(fileDir);
                if (il2cppFiles.success && true/*ConfigurationManager.Settings.UseCpp2Il*/)
                {
                    Manager.MonoTempGenerator = new Cpp2IlTempGenerator(il2cppFiles.metaPath, il2cppFiles.asmPath);
                    return true;
                }
            }
            return false;
        }

        public AssetInst? GetAssetInst(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
        {
            return GetAssetInst(fileInst, pptrField["m_FileID"].AsInt, pptrField["m_PathID"].AsLong);
        }

        public AssetInst? GetAssetInst(AssetsFileInstance fileInst, int fileId, long pathId)
        {
            // todo dupe
            if (fileId != 0)
            {
                fileInst = fileInst.GetDependency(Manager, fileId - 1);
            }
            if (fileInst == null)
            {
                return null;
            }

            AssetFileInfo? info = fileInst.file.GetAssetInfo(pathId);
            if (info == null)
            {
                return null;
            }
            // todo dupe

            if (info is AssetInst inst)
            {
                return inst;
            }
            else if (info is AssetFileInfo)
            {
                return new AssetInst(fileInst, info);
            }

            throw new Exception("Not a valid info!");
        }

        public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
        {
            return GetBaseField(fileInst, pptrField["m_FileID"].AsInt, pptrField["m_PathID"].AsLong);
        }

        public AssetTypeValueField? GetBaseField(AssetInst asset)
        {
            if (asset.BaseValueField != null)
            {
                return asset.BaseValueField;
            }

            var baseField = GetBaseField(asset.FileInstance, asset.PathId);
            asset.BaseValueField = baseField;
            return baseField;
        }

        public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, int fileId, long pathId)
        {
            if (fileId != 0)
            {
                fileInst = fileInst.GetDependency(Manager, fileId - 1);
            }
            if (fileInst == null)
            {
                return null;
            }

            AssetFileInfo? info = fileInst.file.GetAssetInfo(pathId);
            if (info == null)
            {
                return null;
            }

            CheckAndSetMonoTempGenerators(fileInst, info);

            try
            {
                return Manager.GetBaseField(fileInst, info);
            }
            catch
            {
                return null;
            }
        }

        public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, long pathId)
        {
            return GetBaseField(fileInst, 0, pathId);
        }

        public void Dirty(WorkspaceItem item)
        {
            UnsavedItems.Add(item);
            ModifiedItems.Add(item);
            if (item.Parent != null)
            {
                Dirty(item.Parent);
            }
        }

        public void CloseAll()
        {
            Manager.UnloadAll();
            RootItems.Clear();
            ItemLookup.Clear();
            UnsavedItems.Clear();
            ModifiedItems.Clear();
        }
    }
}
