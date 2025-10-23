using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Media.Imaging;
using TexturePlugin.Helpers;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;
using UABEANext4.Plugins;

namespace TexturePlugin;
public class SpritePreviewer : IUavPluginPreviewer
{
    public string Name => "Preview Sprite";
    public string Description => "Preview Sprites";

    private readonly TextureLoader _textureLoader = new();

    public UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection)
    {
        var previewType = selection.Type == AssetClassID.Sprite
            ? UavPluginPreviewerType.Image
            : UavPluginPreviewerType.None;

        return previewType;
    }

    public Bitmap? ExecuteImage(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
    {
        var image = _textureLoader.GetSpriteAvaloniaBitmap(workspace, selection, true, out TextureFormat format);
        if (image != null)
        {
            error = null;
            return image;
        }
        else
        {
            error = $"Sprite texture failed to decode. The image format may not be supported or the texture is not valid. ({format})";
            return null;
        }
    }

    public MeshObj? ExecuteMesh(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
        => throw new InvalidOperationException();

    public string? ExecuteText(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
        => throw new InvalidOperationException();

    public void Cleanup() => _textureLoader.Cleanup();
}
