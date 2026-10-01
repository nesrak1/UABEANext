using AssetsTools.NET.Texture;

namespace TexturePlugin.Helpers;

// Read the stored mipmaps below the main image.
public static class TextureMipDecoder
{
    public static IEnumerable<(int Level, int Width, int Height, byte[] Pixels)> Decode(TextureFile texture, byte[] raw)
    {
        if (texture.m_ImageCount != 1 || texture.m_TextureDimension != 2 || texture.m_StreamingMipmaps)
            throw new NotSupportedException("Mip preview requires a complete, non-streaming 2D texture.");
        if (texture.m_Width <= 0 || texture.m_Height <= 0 || texture.m_MipCount < 1
            || texture.m_MipCount > TextureOperations.GetMaxMipCount(texture.m_Width, texture.m_Height))
            throw new InvalidDataException("Invalid mip dimensions or count.");
        var format = (TextureFormat)texture.m_TextureFormat;
        Ps4MipChain? ps4 = null;
        Ps5MipChain? ps5 = null;
        if (texture.swizzleType == SwizzleType.PS4)
        {
            format = Ps4MortonLayout.GetStorageFormat(format);
            ps4 = Ps4MipChain.ForUnity(texture.m_Width, texture.m_Height, format, texture.m_MipCount, raw.Length);
        }
        else if (texture.swizzleType == SwizzleType.PS5)
        {
            format = Ps5GfxLayout.GetStorageFormat(format);
            ps5 = Ps5MipChain.ForUnity(texture.m_Width, texture.m_Height, format, texture.m_MipCount, raw.Length);
        }
        else if (texture.swizzleType != SwizzleType.None)
            throw new NotSupportedException("Mip preview for this console layout is not implemented.");

        if (!ConsoleTextureElements.TryGetInfo(format, out int block, out int bytes))
        {
            block = 1;
            bytes = format switch
            {
                TextureFormat.RGB24 => 3,
                TextureFormat.RGB48 => 6,
                TextureFormat.RGBFloat => 12,
                _ => 0
            };
            if (bytes == 0)
                throw new NotSupportedException($"Mip preview is not implemented for {format}.");
        }
        int offset = 0;
        for (int mip = 0; mip < texture.m_MipCount; mip++)
        {
            int w = Math.Max(1, texture.m_Width >> mip), h = Math.Max(1, texture.m_Height >> mip);
            int dw = checked(((w + block - 1) / block) * block);
            int dh = checked(((h + block - 1) / block) * block);
            int size = checked(dw / block * (dh / block) * bytes);
            byte[] linear;
            if (ps4 != null)
            {
                if (mip == 0)
                    continue;
                linear = ps4.Levels[mip].Deswizzle(raw.AsSpan(ps4.Offsets[mip], ps4.Levels[mip].TiledSize));
            }
            else if (ps5 != null)
            {
                if (mip == 0)
                    continue;
                linear = ps5.DeswizzleLevel(raw, mip);
            }
            else
            {
                if (offset > raw.Length - size)
                    throw new InvalidDataException($"Missing data for mip {mip}.");
                int start = offset;
                offset = checked(offset + size);
                if (mip == 0)
                    continue;
                linear = raw.AsSpan(start, size).ToArray();
            }
            var decoded = TextureFile.DecodeManagedData(linear, format, dw, dh)
                ?? throw new InvalidDataException($"Could not decode mip {mip}.");
            var cropped = new byte[checked(w * h * 4)];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(decoded, y * dw * 4, cropped, y * w * 4, w * 4);
            yield return (mip, w, h, cropped);
        }
    }
}
