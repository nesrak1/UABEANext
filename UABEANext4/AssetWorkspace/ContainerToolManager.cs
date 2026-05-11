using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using UABEANext4.Util;

namespace UABEANext4.AssetWorkspace;

public class ContainerToolManager
{
    private Workspace _workspace;

    private readonly Dictionary<(string, string), ContainerTool> _bundleContCache = [];
    private readonly Dictionary<string, ContainerTool> _rsrcManContCache = [];

    public ContainerToolManager(Workspace workspace)
    {
        _workspace = workspace;
    }

    public bool TryGetContainerTool(AssetsFileInstance fileInst,
        [MaybeNullWhen(false)] out ContainerTool contTool)
    {
        contTool = null;

        // we can't check parentBundle is not null because it's possible this file
        // has been extracted from a bundle.
        var bundleCacheKey = (AssetsManager.GetBundleLookupKey(fileInst.path), fileInst.name);
        if (_bundleContCache.TryGetValue(bundleCacheKey, out contTool))
            return true;

        // check if bundle has AssetBundle
        var assetBundleInfo = FindFirstAssetOfType(fileInst, (int)AssetClassID.AssetBundle);
        if (assetBundleInfo is not null)
        {
            var contBf = _workspace.GetBaseField(fileInst, assetBundleInfo.PathId);
            if (contBf is null)
                return false; // nothing we can do if it doesn't read right

            contTool = ContainerTool.FromAssetBundle(_workspace.Manager, fileInst, contBf);
            _bundleContCache[bundleCacheKey] = contTool;
            return true;
        }

        // check if globalgamemanagers has it
        var gameDir = PathUtils.GetAssetsFileDirectory(fileInst);
        if (gameDir is null)
            return false;

        var ggmPath = Path.Combine(gameDir, "globalgamemanagers");
        if (!File.Exists(ggmPath))
            return false;

        var rsrcManKey = AssetsManager.GetFileLookupKey(ggmPath);
        if (_rsrcManContCache.TryGetValue(rsrcManKey, out contTool))
            return true;

        // this intentionally does not add to the workspace file list
        // if the user loads this file themselves, it will reuse the
        // currently open ggm file+stream.
        AssetsFileInstance ggmInst;
        int ggmIndex = _workspace.Manager.Files.FindIndex(f => AssetsManager.GetFileLookupKey(f.path) == rsrcManKey);
        if (ggmIndex != -1)
            ggmInst = _workspace.Manager.Files[ggmIndex];
        else
            ggmInst = _workspace.Manager.LoadAssetsFile(ggmPath, true);

        var rsrcManInfo = FindFirstAssetOfType(ggmInst, (int)AssetClassID.ResourceManager);
        if (rsrcManInfo is not null)
        {
            var rsrcManBf = _workspace.GetBaseField(ggmInst, 0, rsrcManInfo.PathId);
            if (rsrcManBf is null)
                return false;

            contTool = ContainerTool.FromResourceManager(_workspace.Manager, ggmInst, rsrcManBf);
            _rsrcManContCache[rsrcManKey] = contTool;
            return true;
        }

        // we checked both, must not have any container data
        return false;
    }

    private AssetFileInfo? FindFirstAssetOfType(AssetsFileInstance file, int classId)
    {
        foreach (var info in file.file.AssetInfos)
        {
            if (info.TypeId == classId)
            {
                return info;
            }
        }

        return null;
    }
}
