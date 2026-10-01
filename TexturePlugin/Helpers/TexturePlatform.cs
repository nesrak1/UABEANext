using UABEANext4.Logic.AssetInfo;
using AssetsTools.NET.Texture;

namespace TexturePlugin.Helpers;

public static class TexturePlatform
{
    // Rearrange the stored data without recompressing it.
    public static void SetPreprocessed(TextureFile texture, uint platform, bool enabled)
    {
        if (texture.m_IsPreProcessed == enabled)
            return;

        bool isPs4 = platform == (uint)BuildTarget.PS4;
        bool isPs5 = platform == (uint)BuildTarget.PS5;
        bool isSwitch = platform == (uint)BuildTarget.Switch;
        if (!isPs4 && !isPs5 && !isSwitch)
            throw new NotSupportedException("Preprocessing conversion is supported only for PS4, PS5 and Switch.");

        if (texture.m_ImageCount != 1 || texture.m_TextureDimension != 2 || texture.m_StreamingMipmaps)
            throw new NotSupportedException("Preprocessing conversion requires a non-streaming 2D texture.");

        var format = (TextureFormat)texture.m_TextureFormat;

        if (isSwitch && (texture.m_MipCount != 1 || texture.m_MipMap))
            throw new NotSupportedException("Switch preprocessing conversion currently requires one mip.");
        if (isSwitch && enabled && texture.m_PlatformBlob.Length != 0)
            throw new NotSupportedException("Switch texture already has platform metadata despite a false preprocessing flag. The storage layout is unclear.");

        if (isSwitch && !enabled)
            format = SwitchSwizzle.GetCorrectedSwitchTextureFormat(format);
        if (isPs4 && !enabled)
            format = Ps4MortonLayout.GetStorageFormat(format);

        if (isPs5 && !enabled)
            format = Ps5GfxLayout.GetStorageFormat(format);

        byte[] source = texture.pictureData ?? throw new InvalidDataException("Load the complete texture data before conversion.");

        if ((isPs4 || isPs5) && enabled && format == TextureFormat.RGB24)
        {
            int linearSize = isPs4
                ? new Ps4MipChain(texture.m_Width, texture.m_Height, format, texture.m_MipCount).LinearSize
                : new Ps5MipChain(texture.m_Width, texture.m_Height, format, texture.m_MipCount).LinearSize;
            int pixels = linearSize / 4;
            if (source.Length != checked(pixels * 3))
                throw new InvalidDataException("Linear RGB24 must contain three bytes per pixel.");
            var rgba = new byte[checked(pixels * 4)];
            for (int i = 0; i < pixels; i++)
            {
                Buffer.BlockCopy(source, i * 3, rgba, i * 4, 3);
                rgba[i * 4 + 3] = 255;
            }
            source = rgba;
        }

        byte[] result;
        byte[] blob = texture.m_PlatformBlob;

        if (isPs4)
        {
            var chain = enabled
                ? new Ps4MipChain(texture.m_Width, texture.m_Height, format, texture.m_MipCount)
                : Ps4MipChain.ForUnity(texture.m_Width, texture.m_Height, format, texture.m_MipCount, source.Length);
            int expected = enabled ? chain.LinearSize : chain.TiledSize;
            if (source.Length != expected)
                throw new InvalidDataException("Texture size does not match the supported PS4 layout.");
            result = enabled
                ? chain.Swizzle(chain.SplitLinear(source))
                : TextureOperations.FlattenMips(chain.Deswizzle(source), out _);
        }
        else if (isPs5)
        {
            var chain = enabled
                ? new Ps5MipChain(texture.m_Width, texture.m_Height, format, texture.m_MipCount)
                : Ps5MipChain.ForUnity(texture.m_Width, texture.m_Height, format, texture.m_MipCount, source.Length);
            result = enabled ? chain.Swizzle(chain.SplitLinear(source))
                : TextureOperations.FlattenMips(chain.Deswizzle(source), out _);
        }
        else
        {
            var layout = new Ps4MortonLayout(texture.m_Width, texture.m_Height, format);
            var block = SwitchSwizzle.GetTextureFormatBlockSize(format);
            if (block.IsEmpty)
                throw new NotSupportedException("Unsupported Switch format.");
            if (!enabled && (blob == null || blob.Length < 12))
                throw new InvalidDataException("Switch platform metadata is missing.");

            int gobHeight = enabled
                ? SwitchSwizzle.GetBlockHeightByBlockSize(block, texture.m_Height)
                : SwitchSwizzle.GetBlockHeightByPlatformBlob(blob);
            var padded = SwitchSwizzle.GetPaddedTextureSize(
                texture.m_Width, texture.m_Height, block.Width, block.Height, gobHeight);
            int stride = checked(padded.Width / block.Width * 16);
            int storedSize = checked(stride * (padded.Height / block.Height));
            int logicalStride = checked(layout.BlocksWide * layout.BytesPerBlock);
            if (source.Length != (enabled ? layout.LinearSize : storedSize))
                throw new InvalidDataException("Texture size does not match the supported Switch layout.");

            if (enabled)
            {
                var paddedData = new byte[storedSize];
                for (int y = 0; y < layout.BlocksHigh; y++)
                    Buffer.BlockCopy(source, y * logicalStride, paddedData, y * stride, logicalStride);
                result = SwitchSwizzle.Swizzle(paddedData, padded, block, gobHeight);
                var swizzler = new SwitchSwizzle(texture.m_Width, texture.m_Height, ref format, out _, out _);
                blob = swizzler.MakePlatformBlob(new[] { 0 }, (uint)result.Length);
            }
            else
            {
                var paddedData = SwitchSwizzle.Unswizzle(source, padded, block, gobHeight);
                result = new byte[layout.LinearSize];
                for (int y = 0; y < layout.BlocksHigh; y++)
                    Buffer.BlockCopy(paddedData, y * stride, result, y * logicalStride, logicalStride);
                // An empty blob also disables the existing Switch detection path.
                blob = Array.Empty<byte>();
            }
        }

        // Update the texture after conversion succeeds.
        texture.pictureData = result;
        texture.m_CompleteImageSize = result.Length;
        texture.m_StreamData.path = "";
        texture.m_StreamData.offset = 0;
        texture.m_StreamData.size = 0;
        texture.m_PlatformBlob = blob;
        texture.m_TextureFormat = (int)format;
        texture.m_IsPreProcessed = enabled;
        texture.swizzleType = GetSwizzleType(texture, platform);
    }

    public static SwizzleType GetSwizzleType(TextureFile texture, uint targetPlatform)
    {
        if (targetPlatform == (uint)BuildTarget.PS4 && texture.m_IsPreProcessed)
            return SwizzleType.PS4;
        if (targetPlatform == (uint)BuildTarget.PS5 && texture.m_IsPreProcessed)
            return SwizzleType.PS5;
        if (targetPlatform == (uint)BuildTarget.Switch && texture.m_PlatformBlob.Length != 0)
            return SwizzleType.Switch;
        return SwizzleType.None;
    }
}
