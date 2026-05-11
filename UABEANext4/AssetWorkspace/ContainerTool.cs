using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UABEANext4.AssetWorkspace;

// from UABEA. probably needs some editing/cleanup.
public class ContainerTool
{
    public List<AssetPPtr> PreloadTable { get; } = [];
    // normally this map is string -> AssetInfo, but we only do path id -> string lookups so this isn't useful
    public Dictionary<ContainerAssetInfo, string> AssetMap { get; } = [];

    public static ContainerTool FromAssetBundle(AssetsManager am, AssetsFileInstance fromFile, AssetTypeValueField assetBundleBf)
    {
        ContainerTool ct = new ContainerTool();

        AssetTypeValueField m_PreloadTable = assetBundleBf["m_PreloadTable.Array"];
        foreach (AssetTypeValueField ptr in m_PreloadTable)
        {
            AssetPPtr assetPPtr = AssetPPtr.FromField(ptr);
            assetPPtr.SetFilePathFromFile(am, fromFile);
            ct.PreloadTable.Add(assetPPtr);
        }

        AssetTypeValueField m_Container = assetBundleBf["m_Container.Array"];
        foreach (AssetTypeValueField container in m_Container)
        {
            string key = container["first"].AsString;
            AssetTypeValueField value = container["second"];

            ContainerAssetInfo assetInfo = ContainerAssetInfo.FromField(value);
            assetInfo.Ptr.SetFilePathFromFile(am, fromFile);
            if (assetInfo.Ptr.PathId != 0)
            {
                ct.AssetMap[assetInfo] = key;
            }
        }

        return ct;
    }

    public static ContainerTool FromResourceManager(AssetsManager am, AssetsFileInstance fromFile, AssetTypeValueField rsrcManBf)
    {
        ContainerTool ct = new ContainerTool();

        AssetTypeValueField m_Container = rsrcManBf["m_Container.Array"];
        foreach (AssetTypeValueField container in m_Container)
        {
            string key = container["first"].AsString;
            AssetTypeValueField value = container["second"];

            AssetPPtr assetPPtr = AssetPPtr.FromField(value);
            assetPPtr.SetFilePathFromFile(am, fromFile);

            ContainerAssetInfo assetInfo = new ContainerAssetInfo(assetPPtr);
            if (assetPPtr.PathId != 0)
            {
                ct.AssetMap[assetInfo] = key;
            }
        }

        return ct;
    }

    public string? GetContainerPath(AssetsFileInstance fileInst, long pathId)
    {
        return GetContainerPath(new AssetPPtr(fileInst.path, 0, pathId));
    }

    public string? GetContainerPath(AssetPPtr assetPPtr)
    {
        ContainerAssetInfo search = new ContainerAssetInfo(assetPPtr);
        if (AssetMap.TryGetValue(search, out string? path))
        {
            return path;
        }

        return null;
    }

    public ContainerAssetInfo GetContainerInfo(string path)
    {
        return AssetMap.FirstOrDefault(i => i.Value.Equals(path, StringComparison.InvariantCultureIgnoreCase)).Key;
    }
}

public class ContainerAssetInfo
{
    public int PreloadIndex;
    public int PreloadSize;
    public AssetPPtr Ptr;
    public object Name;

    public ContainerAssetInfo(AssetPPtr asset)
    {
        PreloadIndex = -1;
        PreloadSize = -1;
        Ptr = asset;
        Name = string.Empty;
    }

    public ContainerAssetInfo(int preloadIndex, int preloadSize, AssetPPtr asset)
    {
        PreloadIndex = preloadIndex;
        PreloadSize = preloadSize;
        Ptr = asset;
        Name = string.Empty;
    }

    public static ContainerAssetInfo FromField(AssetTypeValueField field)
    {
        int preloadIndex = field["preloadIndex"].AsInt;
        int preloadSize = field["preloadSize"].AsInt;
        AssetPPtr asset = AssetPPtr.FromField(field["asset"]);
        return new ContainerAssetInfo(preloadIndex, preloadSize, asset);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ContainerAssetInfo assetInfo)
            return false;

        return assetInfo.Ptr.Equals(Ptr);
    }

    public override int GetHashCode()
    {
        return Ptr.GetHashCode();
    }
}
