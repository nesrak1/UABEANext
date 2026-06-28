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
using UABEANext4.Logic.Configuration;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace UABEANext4.AssetWorkspace;

public partial class Workspace : ObservableObject
{
    public AssetsManager Manager { get; } = new AssetsManager();
    public PluginLoader Plugins { get; } = new PluginLoader();
    public AssetNamer Namer { get; }

    public Mutex ModifyMutex { get; } = new Mutex();

    // this should be its own class
    [ObservableProperty]
    public float _progressValue = 0f;
    [ObservableProperty]
    public string _progressText = "";

    public ObservableCollection<WorkspaceItem> RootItems { get; } = [];
    public Dictionary<string, WorkspaceItem> ItemLookup { get; } = [];
    private SynchronizationContext? FileSyncContext { get; } = SynchronizationContext.Current;

    // items modified and unsaved
    public HashSet<WorkspaceItem> UnsavedItems { get; } = [];
    // items modified and saved
    // we track this since the base AssetsFile is still reading from the old file
    public HashSet<WorkspaceItem> ModifiedItems { get; } = [];

    public int NextLoadIndex => RootItems.Count != 0 ? RootItems.Max(i => i.LoadIndex) + 1 : 0;

    public delegate void MonoTemplateFailureEvent(string path);
    public event MonoTemplateFailureEvent? MonoTemplateLoadFailed;

    // keys of files we're working on but haven't been added to AssetsManager's file list yet
    private HashSet<string> _workingKeys = [];

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

        Namer = new AssetNamer(this);
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

        // keys we'll add to workingKeys to "lock" them
        var ourWorkingKeys = new List<string>();

        // check for duplicate files
        // we stop at the first one since any duplicates are bad
        lock (_workingKeys)
        {
            var dirInfs = bunInst.file.BlockAndDirInfo.DirectoryInfos;
            foreach (var dirInf in dirInfs)
            {
                var dirInfKey = AssetsManager.GetFileLookupKey(dirInf.Name);
                if (Manager.FileLookup.ContainsKey(dirInfKey) || _workingKeys.Contains(dirInfKey))
                {
                    throw new DuplicateWorkspaceFileException(dirInfKey, bunInst.path);
                }

                ourWorkingKeys.Add(dirInfKey);
            }

            // no exception, so we're free to lock in these files now
            foreach (var keyToAdd in ourWorkingKeys)
            {
                _workingKeys.Add(keyToAdd);
            }
        }

        WorkspaceItem item;
        try
        {
            TryLoadClassDatabase(bunInst.file);

            item = new WorkspaceItem(this, bunInst, loadOrder);
            AddRootItemThreadSafe(item, bunInst.name);
        }
        finally
        {
            // done with files, they are now part of AssetsManager
            // so we should let it handle things from now on
            lock (_workingKeys)
            {
                foreach (var keyToRemove in ourWorkingKeys)
                {
                    _workingKeys.Remove(keyToRemove);
                }
            }
        }

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

        // check if file is duplicate
        var fileKey = AssetsManager.GetFileLookupKey(fileInst.path);

        lock (_workingKeys)
        {
            if (Manager.FileLookup.ContainsKey(fileKey) || _workingKeys.Contains(fileKey))
            {
                throw new DuplicateWorkspaceFileException(fileInst.path);
            }

            // no exception, so we're free to lock in this file

            _workingKeys.Add(fileKey);
        }

        WorkspaceItem item;
        try
        {
            TryLoadClassDatabase(fileInst.file);

            FixupAssetsFile(fileInst);

            item = new WorkspaceItem(fileInst, loadOrder);
            AddRootItemThreadSafe(item, fileInst.name);
        }
        finally
        {
            lock (_workingKeys)
            {
                _workingKeys.Remove(fileKey);
            }
        }

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
            var maxNameLen = ConfigurationManager.Settings.ListingNameLength;
            foreach (var info in fileInst.file.AssetInfos)
            {
                var asset = new AssetInst(fileInst, info);
                asset.AssetName = Namer.GetAssetName(asset, true, maxNameLen);

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
            FindCpp2IlFilesResult il2cppFiles = FindCpp2IlFiles.Find(fileDir);

            bool managedExists = Directory.Exists(managedDir);
            bool il2cppExists = il2cppFiles.success;

            if (managedExists && (!il2cppExists || ConfigurationManager.Settings.UseManagedOverIl2cpp))
            {
                bool hasDll = Directory.GetFiles(managedDir, "*.dll").Length > 0;
                if (hasDll)
                {
                    Manager.MonoTempGenerator = new MonoCecilTempGenerator(managedDir);
                    return true;
                }
            }

            if (il2cppExists)
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

    public AssetTypeValueField? GetBaseField(AssetInst asset)
    {
        return GetBaseField(asset.FileInstance, asset.PathId);
    }

    public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, long pathId)
    {
        return GetBaseField(fileInst, 0, pathId);
    }

    public AssetTypeValueField? GetBaseField(AssetsFileInstance fileInst, AssetTypeValueField pptrField)
    {
        return GetBaseField(fileInst, pptrField["m_FileID"].AsInt, pptrField["m_PathID"].AsLong);
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
        catch (ObjectDisposedException)
        {
            // rethrow exceptions caused by file being closed. we want to know about
            // these since they are different than just "asset failed to read".
            throw;
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

    public void Close(WorkspaceItem item)
    {
        if (!item.Loaded || !RootItems.Contains(item))
            return;

        var itemObj = item.Object;
        if (item.ObjectType == WorkspaceItemType.ResourceFile)
        {
            var stream = (Stream)itemObj;
            stream.Close();
        }
        else if (item.ObjectType == WorkspaceItemType.BundleFile)
        {
            var bunInst = (BundleFileInstance)itemObj;
            Manager.UnloadBundleFile(bunInst);
        }
        else if (item.ObjectType == WorkspaceItemType.AssetsFile && item.Parent is null)
        {
            var fileInst = (AssetsFileInstance)itemObj;
            Manager.UnloadAssetsFile(fileInst);
        }

        RootItems.Remove(item);
        ItemLookup.Remove(item.Name);
        UnsavedItems.Remove(item);
        ModifiedItems.Remove(item);

        // we currently don't support more than one level of children
        foreach (var childItem in item.Children)
        {
            ItemLookup.Remove(item.Name);
            UnsavedItems.Remove(childItem);
            ModifiedItems.Remove(childItem);
        }
    }

    public void CloseAll()
    {
        foreach (var item in RootItems)
        {
            if (item.ObjectType == WorkspaceItemType.ResourceFile && item.Loaded)
            {
                var stream = (Stream)item.Object;
                stream.Close();
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

    public WorkspaceItem? FindWorkspaceItemByInstance(AssetsFileInstance fileInst)
    {
        // todo: keying needs to be a generic method
        var key = fileInst.name;

        if (ItemLookup.TryGetValue(key, out var wsItem))
            return wsItem;

        // no match? try bfs searching starting at the root
        // we pass the null since this is the last resort option
        return FindWorkspaceItemBfs(i =>
            i.Object is AssetsFileInstance thisFileInst && thisFileInst == fileInst
        );
    }

    public WorkspaceItem? FindWorkspaceItemByInstance(BundleFileInstance bunInst)
    {
        // todo: keying needs to be a generic method
        var key = bunInst.name;

        if (ItemLookup.TryGetValue(key, out var wsItem))
            return wsItem;

        return FindWorkspaceItemBfs(i =>
            i.Object is BundleFileInstance thisBunInst && thisBunInst == bunInst
        );
    }

    private WorkspaceItem? FindWorkspaceItemBfs(Func<WorkspaceItem, bool> predicate)
    {
        var searchQueue = new Queue<WorkspaceItem>(RootItems);
        while (searchQueue.Count > 0)
        {
            var current = searchQueue.Dequeue();

            if (predicate(current))
                return current;

            if (current.Children != null)
            {
                foreach (var child in current.Children)
                    searchQueue.Enqueue(child);
            }
        }

        return null;
    }
}
