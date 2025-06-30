using AssetsTools.NET.Extra;
using Avalonia.Media.Imaging;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;
using UABEANext4.Plugins;

namespace TextAssetPlugin;
public class TextAssetPreviewer : IUavPluginPreviewer
{
    public string Name => "Preview TextAsset";
    public string Description => "Preview TextAssets";

    const int TEXT_ASSET_MAX_LENGTH = 100000;

    public UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection)
    {
        var previewType = selection.Type == AssetClassID.TextAsset
            ? UavPluginPreviewerType.Text
            : UavPluginPreviewerType.None;

        return previewType;
    }

    public string? ExecuteText(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
    {
        try
        {
            var textAssetBf = workspace.GetBaseField(selection);
            if (textAssetBf == null)
            {
                error = "No preview available.";
                return null;
            }

            var text = textAssetBf["m_Script"].AsByteArray;
            string trimmedText;
            if (text.Length <= TEXT_ASSET_MAX_LENGTH)
            {
                trimmedText = Encoding.UTF8.GetString(text);
            }
            else
            {
                trimmedText = Encoding.UTF8.GetString(text[..TEXT_ASSET_MAX_LENGTH]) + $"... (and {text.Length - TEXT_ASSET_MAX_LENGTH} bytes more)";
            }

            error = null;
            return trimmedText;
        }
        catch (Exception ex)
        {
            error = $"TextAsset failed to decode due to an error. Exception:\n{ex}";
            return null;
        }
    }

    public Bitmap? ExecuteImage(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
        => throw new InvalidOperationException();

    public MeshObj? ExecuteMesh(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
        => throw new InvalidOperationException();

    public void Cleanup() { }
}
