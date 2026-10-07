using AssetsTools.NET;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Util;

internal static class AssetReaderFactory
{
    /// <summary>
    /// Gets an AssetsFileReader and corresponding object to lock on for a given asset instance
    /// </summary>
    /// <param name="asset">The asset to be read</param>
    /// <param name="lockObj">The object that a lock should be held on for parallel operations</param>
    /// <param name="reader">The reader to be used for reading</param>
    /// <param name="pos">The position in the reader where the start of the asset instance can be found</param>
    public static void GetLockObjAndReader(
        AssetInst asset, out object lockObj, out AssetsFileReader reader, out long pos)
    {
        var fileInst = asset.FileInstance;
        if (asset.IsReplacerPreviewable)
        {
            var stream = asset.Replacer.GetPreviewStream();
            lockObj = stream;
            reader = new AssetsFileReader(stream);
            pos = 0;
        }
        else
        {
            lockObj = fileInst.LockReader;
            var baseStream = asset.FileInstance.file.Reader.BaseStream;
            var segStream = new SegmentStream(baseStream, asset.AbsoluteByteStart, asset.ByteSize);
            reader = new AssetsFileReader(segStream);
            pos = 0;
        }
    }
}