using AssetsTools.NET;
using UABEANext4.AssetWorkspace;

namespace FontPlugin;

public static class FontHelper
{
    public static AssetTypeValueField? GetByteArrayFont(Workspace workspace, AssetInst asset)
    {
        AssetTypeTemplateField? fontTemp = workspace.GetTemplateField(asset);
        if (fontTemp == null)
            return null;

        AssetTypeTemplateField? fontData = fontTemp.Children.FirstOrDefault(f => f.Name == "m_FontData");
        if (fontData == null)
            return null;

        // m_FontData.Array
        fontData.Children[0].ValueType = AssetValueType.ByteArray;

        AssetTypeValueField baseField = fontTemp.MakeValue(asset.FileReader, asset.AbsoluteByteStart);
        return baseField;
    }

    public static bool IsDataOtf(byte[] byteData)
    {
        return byteData[0] == 0x4f &&
            byteData[1] == 0x54 &&
            byteData[2] == 0x54 &&
            byteData[3] == 0x4f;
    }
}