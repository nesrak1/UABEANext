using AssetsTools.NET.Extra;
using System.Text;
using UABEANext4.AssetWorkspace;
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

    public PreviewResult Execute(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection)
    {
        try
        {
            var textAssetBf = workspace.GetBaseField(selection);
            if (textAssetBf == null)
            {
                return new PreviewResult.Error("No preview available.");
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

            return new PreviewResult.Text(trimmedText);
        }
        catch (Exception ex)
        {
            string error = $"TextAsset failed to decode due to an error. Exception:\n{ex}";
            return new PreviewResult.Error(error);
        }
    }

    public void Cleanup() { }
}
