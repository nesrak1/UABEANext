using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using UABEANext4.AssetWorkspace;

namespace TexturePlugin;

public static class TextureHelper
{
    public static AssetTypeValueField? GetByteArrayTexture(Workspace workspace, AssetInst tex)
    {
        var textureTemp = workspace.GetTemplateField(tex);
        var image_data = textureTemp.Children.FirstOrDefault(f => f.Name == "image data");
        if (image_data == null)
            return null;

        image_data.ValueType = AssetValueType.ByteArray;

        var m_PlatformBlob = textureTemp.Children.FirstOrDefault(f => f.Name == "m_PlatformBlob");
        if (m_PlatformBlob != null)
        {
            var m_PlatformBlob_Array = m_PlatformBlob.Children[0];
            m_PlatformBlob_Array.ValueType = AssetValueType.ByteArray;
        }

        var baseField = textureTemp.MakeValue(tex.FileReader, tex.AbsoluteByteStart);
        return baseField;
    }

    public static byte[]? GetRawTextureBytes(TextureFile texFile, AssetsFileInstance inst)
    {
        var rootPath = Path.GetDirectoryName(inst.path);
        if (texFile.m_StreamData.size != 0 && texFile.m_StreamData.path != string.Empty)
        {
            string fixedStreamPath = texFile.m_StreamData.path;
            if (inst.parentBundle == null && fixedStreamPath.StartsWith("archive:/"))
            {
                fixedStreamPath = Path.GetFileName(fixedStreamPath);
            }
            if (!Path.IsPathRooted(fixedStreamPath) && rootPath != null)
            {
                fixedStreamPath = Path.Combine(rootPath, fixedStreamPath);
            }
            if (File.Exists(fixedStreamPath))
            {
                using Stream stream = File.OpenRead(fixedStreamPath);
                stream.Position = (long)texFile.m_StreamData.offset;
                texFile.pictureData = new byte[texFile.m_StreamData.size];
                stream.Read(texFile.pictureData, 0, (int)texFile.m_StreamData.size);
            }
            else
            {
                return null;
            }
        }
        return texFile.pictureData;
    }

    public static byte[]? GetPlatformBlob(AssetTypeValueField texBaseField)
    {
        var m_PlatformBlob = texBaseField["m_PlatformBlob"];
        if (!m_PlatformBlob.IsDummy)
        {
            return m_PlatformBlob["Array"].AsByteArray;
        }
        return null;
    }

    public static bool IsPo2(int n)
    {
        return n > 0 && ((n & (n - 1)) == 0);
    }

    // assuming width and height are po2
    public static int GetMaxMipCount(int width, int height)
    {
        int widthMipCount = (int)Math.Log2(width) + 1;
        int heightMipCount = (int)Math.Log2(height) + 1;
        // if the texture is 512x1024 for example, select the height (1024)
        // I guess the width would stay 1 while the height resizes down
        return Math.Max(widthMipCount, heightMipCount);
    }
}
