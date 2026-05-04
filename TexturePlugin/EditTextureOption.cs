using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using System.Text;
using TexturePlugin.Helpers;
using TexturePlugin.ViewModels;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;

namespace TexturePlugin;

public class EditTextureOption : IUavPluginOption
{
    public string Name => "Edit Texture2D";
    public string Description => "Edits Texture2D settings";
    public UavPluginMode Options => UavPluginMode.Export;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Export)
        {
            return false;
        }

        var texTypeId = (int)AssetClassID.Texture2D;
        return selection.All(a => a.TypeId == texTypeId);
    }

    public async Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection)
    {
        var editTextureVm = new EditTextureViewModel(workspace, funcs, selection);
        var result = await funcs.ShowDialog(editTextureVm);
        if (!result.HasValue)
        {
            return false;
        }

        var editTexSettings = result.Value;

        var singleTextureEdit = selection.Count == 1 && editTexSettings.LoadTexturePath is not null;

        var errorBuilder = new StringBuilder();
        foreach (var asset in selection)
        {
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";

            var baseField = TextureHelper.GetByteArrayTexture(workspace, asset);
            if (baseField == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                continue;
            }

            var tex = TextureFile.ReadTextureFile(baseField);
            if (tex.m_PlatformBlob.Length != 0)
            {
                TextureHelper.SwizzleOptIn(tex, asset.FileInstance.file);
            }

            byte[]? texOrigDecBytes = null;
            // single texture edit _replaces_ the original, so no
            // need to try and decompress the original texture.
            if (!singleTextureEdit)
            {
                var needToReencode = false;
                if (editTexSettings.TextureFormat is not null)
                {
                    needToReencode |= tex.m_TextureFormat != (int)editTexSettings.TextureFormat;
                }
                if (editTexSettings.UsingMips is not null)
                {
                    // if we've toggled mips on, only make a change if the current
                    // mipcount is different from what we would change it to.
                    var usingMips = editTexSettings.UsingMips.Value;
                    if (usingMips)
                        needToReencode |= tex.m_MipCount != 1;
                    else
                        needToReencode |= tex.m_MipCount == 1;
                }

                if (needToReencode)
                {
                    // decode the texture so we can reencode it in the next step
                    var texOrigEncBytes = tex.FillPictureData(asset.FileInstance);
                    if (texOrigEncBytes is null)
                    {
                        errorBuilder.AppendLine($"[{errorAssetName}]: failed to decode for reencoding");
                        continue;
                    }
                    else
                    {
                        texOrigDecBytes = tex.DecodeTextureRaw(texOrigEncBytes, true);
                        // flip now because encoder will flip but raw doesn't flip
                        TextureOperations.FlipBGRA32VerticallyInplace(texOrigDecBytes, tex.m_Width, tex.m_Height);
                    }
                }
            }

            if (editTexSettings.Name is not null)
                tex.m_Name = editTexSettings.Name;
            if (editTexSettings.TextureFormat is not null)
                tex.m_TextureFormat = (int)editTexSettings.TextureFormat.Value;
            if (editTexSettings.UsingMips is not null)
            {
                tex.m_MipMap = editTexSettings.UsingMips.Value;
                tex.m_MipCount = int.MaxValue; // will get lowered to correct mip count later
            }
            if (editTexSettings.IsReadable is not null)
                tex.m_IsReadable = editTexSettings.IsReadable.Value;
            if (editTexSettings.FilterMode is not null)
                tex.m_TextureSettings.m_FilterMode = (int)editTexSettings.FilterMode.Value;
            if (editTexSettings.Filtering is not null)
                tex.m_TextureSettings.m_Aniso = editTexSettings.Filtering.Value;
            if (editTexSettings.MipBias is not null)
                tex.m_TextureSettings.m_MipBias = editTexSettings.MipBias.Value;
            if (editTexSettings.WrapModeU is not null)
                tex.m_TextureSettings.m_WrapU = (int)editTexSettings.WrapModeU.Value;
            if (editTexSettings.WrapModeV is not null)
                tex.m_TextureSettings.m_WrapV = (int)editTexSettings.WrapModeV.Value;
            if (editTexSettings.LightMapFormat is not null)
                tex.m_LightmapFormat = editTexSettings.LightMapFormat.Value;
            if (editTexSettings.ColorSpace is not null)
                tex.m_ColorSpace = (int)editTexSettings.ColorSpace.Value;

            const int EncodingQualityLevel = 3; // should be configurable at some point
            if (texOrigDecBytes is not null)
            {
                try
                {
                    tex.EncodeTextureRaw(
                        texOrigDecBytes, tex.m_Width, tex.m_Height,
                        mipCount: tex.m_MipCount, quality: EncodingQualityLevel, useBgra: true);
                }
                catch (Exception e)
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: failed to import: {e}");
                }
            }
            else if (singleTextureEdit)
            {
                try
                {
                    tex.EncodeTextureImage(
                        editTexSettings.LoadTexturePath, mipCount: tex.m_MipCount, quality: EncodingQualityLevel);
                }
                catch (Exception e)
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: failed to import: {e}");
                }
            }

            tex.WriteTo(baseField);
            asset.UpdateAssetDataAndRow(workspace, baseField);
        }

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Error", firstLinesStr);
        }

        return true;
    }
}
