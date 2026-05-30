using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using TexturePlugin.Helpers;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;

namespace TexturePlugin;
public class TexturePreviewer : IUavPluginPreviewer
{
    public string Name => "Preview Texture2D";
    public string Description => "Preview Texture2Ds";

    public UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection)
    {
        var previewType = selection.Type == AssetClassID.Texture2D
            ? UavPluginPreviewerType.Image
            : UavPluginPreviewerType.None;

        return previewType;
    }

    public PreviewResult Execute(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection)
    {
        try
        {
            var image = TextureLoader.GetTexture2DBitmap(workspace, selection, out TextureFormat format);
            if (image != null)
            {
                return new PreviewResult.Image(image, (int)format);
            }
            else
            {
                string error = $"Texture failed to decode. The image format may not be supported or the texture is not valid. ({format})";
                return new PreviewResult.Error(error);
            }
        }
        catch (Exception ex)
        {
            string error = $"Texture failed to decode due to an error. Exception:\n{ex}";
            return new PreviewResult.Error(error);
        }
    }

   
    public void Cleanup() { }
}
