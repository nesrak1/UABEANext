namespace UABEANext4.AssetWorkspace;

internal static class UnityFsBundlePadding
{
    // Some UnityFS bundles place a 1 KiB padding block before the compressed data
    // without including it in TotalFileSize or the block offsets.
    private const long KnownDataPaddingSize = 1024;

    internal static bool TryGetAdjustedDataOffset(
        long declaredFileSize,
        long actualFileSize,
        long declaredDataOffset,
        long compressedBlocksSize,
        out long adjustedDataOffset)
    {
        adjustedDataOffset = declaredDataOffset;

        if (actualFileSize < declaredFileSize ||
            actualFileSize - declaredFileSize != KnownDataPaddingSize ||
            declaredDataOffset < 0 ||
            declaredDataOffset > declaredFileSize ||
            compressedBlocksSize < 0 ||
            compressedBlocksSize != declaredFileSize - declaredDataOffset ||
            compressedBlocksSize != actualFileSize - declaredDataOffset - KnownDataPaddingSize)
        {
            return false;
        }

        adjustedDataOffset = declaredDataOffset + KnownDataPaddingSize;
        return true;
    }
}
