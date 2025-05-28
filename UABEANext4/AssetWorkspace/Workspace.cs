using AssetsTools.NET;
using AssetsTools.NET.Cpp2IL;
using AssetsTools.NET.Extra;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace UABEANext4.AssetWorkspace;

public partial class Workspace : ObservableObject
{
    public AssetsManager Manager { get; } = new AssetsManager();
    public PluginLoader Plugins { get; } = new PluginLoader();

    public Mutex ModifyMutex { get; } = new Mutex();

    // this should be its own class
    [ObservableProperty]
    public float _progressValue = 0f;
    [ObservableProperty]
    public string _progressText = "";

    public ObservableCollection<WorkspaceItem> RootItems { get; } = new();
    public Dictionary<string, WorkspaceItem> ItemLookup { get; } = new();
    private SynchronizationContext? FileSyncContext { get; } = SynchronizationContext.Current;

    // items modified and unsaved
    public HashSet<WorkspaceItem> UnsavedItems { get; } = new();
    // items modified and saved
    // we track this since the base AssetsFile is still reading from the old file
    public HashSet<WorkspaceItem> ModifiedItems { get; } = new();

    public int NextLoadIndex => RootItems.Count != 0 ? RootItems.Max(i => i.LoadIndex) + 1 : 0;

    public delegate void MonoTemplateFailureEvent(string path);
    public event MonoTemplateFailureEvent? MonoTemplateLoadFailed;

    private bool _setMonoTempGeneratorsYet;

    public Workspace()
    {
        string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
        if (File.Exists(classDataPath))
            Manager.LoadClassPackage(classDataPath);

        string pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        Plugins.LoadPluginsInDirectory(pluginsPath);

        Manager.UseRefTypeManagerCache = true;
        Manager.UseTemplateFieldCache = true;
        Manager.UseQuickLookup = true;
    }

    public WorkspaceItem? LoadAnyFile(Stream stream, int loadOrder = -1, string path = "")
    {
        if (path == "" && stream is FileStream fs)
        {
            path = fs.Name;
        }

        var detectedType = FileTypeDetector.DetectFileType(new AssetsFileReader(stream), 0);
        if (detectedType == DetectedFileType.BundleFile)
        {
            stream.Position = 0;
            return LoadBundle(stream, loadOrder);
        }
        else if (detectedType == DetectedFileType.AssetsFile)
        {
            stream.Position = 0;
            return LoadAssets(stream, loadOrder);
        }
        else if (path.EndsWith(".resS") || path.EndsWith(".resource"))
        {
            return LoadResource(stream, loadOrder);
        }

        return null;
    }

    public WorkspaceItem LoadBundle(Stream stream, int loadOrder = -1, string name = "")
    {
        // todo: don't always unpack to memory lol
        BundleFileInstance bunInst;
        if (stream is FileStream fs)
        {
            bunInst = Manager.LoadBundleFile(fs);
        }
        else
        {
            bunInst = Manager.LoadBundleFile(stream, name);
        }

        TryLoadClassDatabase(bunInst.file);

        var item = new WorkspaceItem(this, bunInst, loadOrder);
        AddRootItemThreadSafe(item, bunInst.name);

        return item;
    }

    public WorkspaceItem LoadAssets(Stream stream, int loadOrder = -1, string name = "")
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

        TryLoadClassDatabase(fileInst.file);

        FixupAssetsFile(fileInst);

        var item = new WorkspaceItem(fileInst, loadOrder);
        AddRootItemThreadSafe(item, fileInst.name);

        return item;
    }

    public WorkspaceItem LoadAssetsFromBundle(BundleFileInstance bunInst, int index)
    {
        var dirInf = BundleHelper.GetDirInfo(bunInst.file, index);
        var fileInst = Manager.LoadAssetsFileFromBundle(bunInst, index);

        TryLoadClassDatabase(fileInst.file);

        FixupAssetsFile(fileInst);

        var item = new WorkspaceItem(dirInf.Name, fileInst, -1, WorkspaceItemType.AssetsFile);
        return item;
    }

    private void FixupAssetsFile(AssetsFileInstance fileInst)
    {
        if (fileInst.file.AssetInfos is not RangeObservableCollection<AssetFileInfo>)
        {
            var assetInsts = new RangeObservableCollection<AssetFileInfo>();
            var tmp = new List<AssetFileInfo>();
            foreach (var info in fileInst.file.AssetInfos)
            {
                var asset = new AssetInst(fileInst, info);
                lock (asset.FileInstance.LockReader)
                {
                    AssetNameUtils.GetDisplayNameFast(this, asset, true, out string? assetName, out string _);
                    asset.AssetName = assetName;
                }
                tmp.Add(asset);
            }
            assetInsts.AddRange(tmp);
            fileInst.file.Metadata.AssetInfos = assetInsts;
            fileInst.file.GenerateQuickLookup();
        }
    }

    public void TryLoadClassDatabase(AssetBundleFile file)
    {
        if (Manager.ClassDatabase == null)
        {
            var fileVersion = file.Header.EngineVersion;
            if (fileVersion != "0.0.0")
            {
                Manager.LoadClassDatabaseFromPackage(fileVersion);
            }
        }
    }

    public void TryLoadClassDatabase(AssetsFile file)
    {
        if (Manager.ClassDatabase == null)
        {
            var metadata = file.Metadata;
            var fileVersion = metadata.UnityVersion;
            if (fileVersion != "0.0.0")
            {
                Manager.LoadClassDatabaseFromPackage(fileVersion);
            }
        }
    }

    public WorkspaceItem LoadResource(Stream stream, int loadOrder = -1, string name = "")
    {
        if (name == "" && stream is FileStream fs)
        {
            name = Path.GetFileName(fs.Name);
        }

        WorkspaceItem item = new WorkspaceItem(name, stream, loadOrder, WorkspaceItemType.ResourceFile);
        AddRootItemThreadSafe(item, name);

        return item;
    }

    internal void AddRootItemThreadSafe(WorkspaceItem item, string itemName)
    {
        FileSyncContext?.Post(_ =>
        {
            if (item.LoadIndex != -1)
            {
                int pos = RootItems.BinarySearch(item, (i, j) => i.LoadIndex.CompareTo(j.LoadIndex));
                if (pos < 0)
                {
                    RootItems.Insert(~pos, item);
                }
                else
                {
                    RootItems.Insert(pos, item);
                }
                ItemLookup[itemName] = item;
                return;
            }

            RootItems.Add(item);
            ItemLookup[itemName] = item;
        }, null);
    }

    internal void AddChildItemThreadSafe(WorkspaceItem item, WorkspaceItem parent, string itemName)
    {
        FileSyncContext?.Post(_ =>
        {
            // loadorder ignored here
            parent.Children.Add(item);
            item.Parent = parent;
            ItemLookup[itemName] = item;
        }, null);
    }

    public void SetProgressThreadSafe(float value, string text)
    {
        var roundedValue = (float)Math.Round(value * 20) / 20;
        if (Math.Abs(roundedValue - ProgressValue) >= 0.05f || value == 0f || value == 1f)
        {
            FileSyncContext?.Post(_ =>
            {
                ProgressValue = value;
                ProgressText = text;
            }, null);
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

    public void CheckAndSetMonoTempGenerators(AssetsFileInstance fileInst, AssetFileInfo? info)
    {
        bool isValidMono = info == null || info.TypeId == (int)AssetClassID.MonoBehaviour || info.TypeId < 0;
        if (isValidMono && !_setMonoTempGeneratorsYet && !fileInst.file.Metadata.TypeTreeEnabled)
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
                bool hasDll = Directory.GetFiles(managedDir, "*.dll").Length > 0;
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

    public AssetFileInfo? GetAssetFileInfo(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
    {
        return GetAssetFileInfo(fileInst, pptrField["m_FileID"].AsInt, pptrField["m_PathID"].AsLong);
    }

    public AssetFileInfo? GetAssetFileInfo(AssetsFileInstance fileInst, int fileId, long pathId)
    {
        if (fileId != 0)
        {
            fileInst = fileInst.GetDependency(Manager, fileId - 1);
        }
        if (fileInst == null)
        {
            return null;
        }

        return fileInst.file.GetAssetInfo(pathId);
    }

    public AssetInst? GetAssetInst(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
    {
        return GetAssetInst(fileInst, pptrField["m_FileID"].AsInt, pptrField["m_PathID"].AsLong);
    }

    public AssetInst? GetAssetInst(AssetsFileInstance fileInst, int fileId, long pathId)
    {
        if (fileId != 0)
        {
            fileInst = fileInst.GetDependency(Manager, fileId - 1);
            fileId = 0;
        }
        AssetFileInfo? info = GetAssetFileInfo(fileInst, fileId, pathId);

        if (info == null)
        {
            return null;
        }
        else if (info is AssetInst inst)
        {
            return inst;
        }
        else
        {
            return new AssetInst(fileInst, info);
        }
    }

    public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
    {
        return GetBaseField(fileInst, pptrField["m_FileID"].AsInt, pptrField["m_PathID"].AsLong);
    }

    public AssetTypeValueField? GetBaseField(AssetInst asset)
    {
        // todo cache latest n base fields in workspace?
        //if (asset.BaseValueField != null)
        //{
        //    return asset.BaseValueField;
        //}
        //
        //var baseField = GetBaseField(asset.FileInstance, asset.PathId);
        //asset.BaseValueField = baseField;
        return GetBaseField(asset.FileInstance, asset.PathId);
    }

    public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, long pathId)
    {
        return GetBaseField(fileInst, 0, pathId);
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

        // negative target platform seems to indicate an editor version
        AssetReadFlags readFlags = AssetReadFlags.None;
        if ((int)fileInst.file.Metadata.TargetPlatform < 0)
        {
            readFlags |= AssetReadFlags.PreferEditor;
        }

        try
        {
            return Manager.GetBaseField(fileInst, info, readFlags);
        }
        catch
        {
            return null;
        }
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
        foreach (var item in RootItems)
        {
            if (item.ObjectType == WorkspaceItemType.ResourceFile && item.Loaded)
            {
                var stream = (Stream?)item.Object;
                stream?.Close();
            }
        }
        Manager.UnloadAll();
        Manager.UnloadClassDatabase();
        Manager.MonoTempGenerator = null;
        _setMonoTempGeneratorsYet = false;
        RootItems.Clear();
        ItemLookup.Clear();
        UnsavedItems.Clear();
        ModifiedItems.Clear();
    }

    public void RenameFile(WorkspaceItem wsItem, string newName)
    {
        var oldName = wsItem.Name;
        if (oldName != newName)
        {
            if (wsItem.Object is AssetsFileInstance fileInst)
            {
                fileInst.name = newName;
            }
            else if (wsItem.Object is BundleFileInstance bunInst)
            {
                bunInst.name = newName;
            }

            wsItem.Name = newName;
            wsItem.Update(nameof(wsItem.Name));
            Dirty(wsItem);
            ItemLookup.Remove(oldName);
            ItemLookup[newName] = wsItem;
        }
    }
}
