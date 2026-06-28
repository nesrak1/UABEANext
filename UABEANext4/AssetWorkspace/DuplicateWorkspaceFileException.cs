using AssetsTools.NET.Extra;
using System;

namespace UABEANext4.AssetWorkspace;

// temporary fix for duplicate files: just throw an exception and
// don't let the manager fully load the file. in the case of a
// bundle, we'll unload the bundle before any damage occurs from
// parsing each file inside.
// type tree blobs are loaded during bundle creation in the manager,
// but since duplicate blobs are discarded, it also won't cause any
// damage at this time.
public class DuplicateWorkspaceFileException : Exception
{
    public DuplicateLoadInfo Info { get; }

    public DuplicateWorkspaceFileException(string filePath, string bundlePath = "")
    {
        Info = new DuplicateLoadInfo(filePath, bundlePath);
    }

    public override string Message => Info.BundlePath == string.Empty
        ? $"A file with key '{Info.FileKey}' is already loaded."
        : $"A file with key '{Info.FileKey}' from bundle '{Info.BundlePath}' is already loaded.";
}

public sealed record DuplicateLoadInfo(string FilePath, string? BundlePath)
{
    public string FileKey => AssetsManager.GetFileLookupKey(FilePath);

    public string DisplayLine => BundlePath == string.Empty
        ? FileKey
        : $"{FileKey} loaded from {BundlePath}";
}