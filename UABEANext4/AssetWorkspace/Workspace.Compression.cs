using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;

namespace UABEANext4.AssetWorkspace;

public partial class Workspace
{
    public void CompressBundleToFile(
        WorkspaceItem bundleItem,
        string outputPath,
        AssetBundleCompressionType compressionType,
        IAssetBundleCompressProgress? progress = null)
    {
        if (bundleItem.ObjectType != WorkspaceItemType.BundleFile || bundleItem.Object is not BundleFileInstance)
        {
            throw new InvalidOperationException("Selected workspace item is not a bundle.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Output path is empty.");
        }

        string? outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using MemoryStream uncompressedBundleStream = new();
        WriteBundleFile(bundleItem, uncompressedBundleStream);
        uncompressedBundleStream.Position = 0;

        AssetBundleFile bundleToPack = new();
        bundleToPack.Read(new AssetsFileReader(uncompressedBundleStream));

        using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using AssetsFileWriter writer = new(fs);
        bundleToPack.Pack(writer, compressionType, true, progress);
    }
}
