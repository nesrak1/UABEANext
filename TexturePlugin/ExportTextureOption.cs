using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform.Storage;
using System.Text;
using TexturePlugin.Helpers;
using TexturePlugin.ViewModels;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace TexturePlugin;

public class ExportTextureOption : IUavPluginOption
{
    public string Name => "Export Texture2D/Sprite";
    public string Description => "Exports Texture2D/Sprites to png/tga/bmp/jpg";
    public UavPluginMode Options => UavPluginMode.Export;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Export)
        {
            return false;
        }

        var texTypeId = (int)AssetClassID.Texture2D;
        var sprTypeId = (int)AssetClassID.Sprite;
        return selection.All(a => a.TypeId == texTypeId || a.TypeId == sprTypeId);
    }

    public async Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (selection.Count > 1)
        {
            return await BatchExport(workspace, funcs, selection);
        }
        else
        {
            return await SingleExport(workspace, funcs, selection);
        }
    }

    public async Task<bool> BatchExport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        ExportBatchOptionsViewModel dialog = new ExportBatchOptionsViewModel();
        ExportBatchOptionsResult? optionsRes = await funcs.ShowDialog(dialog);

        // bug fix for double dialog box freezing in windows
        await Task.Yield();

        if (optionsRes == null)
        {
            return false;
        }

        string fileExtension = optionsRes.Value.Extension;
        ImageExportType exportType = optionsRes.Value.ImageType;

        var dir = await funcs.ShowOpenFolderDialog(new FolderPickerOpenOptions()
        {
            Title = "Select export directory"
        });

        if (dir == null)
        {
            return false;
        }

        TextureLoader texLoader = new TextureLoader();
        StringBuilder errorBuilder = new StringBuilder();
        int emptyTextureCount = 0;

        foreach (AssetInst asset in selection)
        {
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            if (asset.Type == AssetClassID.Texture2D)
            {
                // we don't need any processing, use assetstools.net.texture to export

                var texBaseField = TextureHelper.GetByteArrayTexture(workspace, asset);
                if (texBaseField == null)
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                    continue;
                }

                var texFile = TextureFile.ReadTextureFile(texBaseField);

                TextureHelper.SwizzleOptIn(texFile, asset.FileInstance.file);

                // 0x0 texture, usually called like Font Texture or something
                if (texFile.m_Width == 0 && texFile.m_Height == 0)
                {
                    emptyTextureCount++;
                    continue;
                }

                string assetName = PathUtils.ReplaceInvalidPathChars(asset.AssetName ?? "Texture2D");
                string filePath = AssetNameUtils.GetAssetFileName(asset, assetName, fileExtension);

                using FileStream outputStream = File.OpenWrite(Path.Combine(dir, filePath));
                byte[] encTextureData = texFile.FillPictureData(asset.FileInstance);
                bool success = texFile.DecodeTextureImage(encTextureData, outputStream, exportType);
                if (!success)
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: failed to decode or write image to disk (missing resS, invalid texture format, etc.)");
                }
            }
            else if (asset.Type == AssetClassID.Sprite)
            {
                // need to do crop processing, use TextureLoader

                byte[]? decTextureData = texLoader.GetSpriteRawBytes(workspace, asset, out var _, out var width, out var height);
                if (decTextureData == null)
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: failed to decode (missing resS, invalid texture format, invalid sprite, etc.)");
                    continue;
                }

                string assetName = PathUtils.ReplaceInvalidPathChars(asset.AssetName ?? "Sprite");
                string filePath = AssetNameUtils.GetAssetFileName(asset, assetName, fileExtension);

                // SKBitmap is RGBA32 but StbIws expects BGRA32. swap R and B.
                TextureOperations.SwapRBComponents(decTextureData);

                // image is also upside down. flip it (normally assetstools.net.texture handles this)
                TextureOperations.FlipBGRA32Vertically(decTextureData, width, height);

                using FileStream outputStream = File.OpenWrite(Path.Combine(dir, filePath));
                if (!TextureOperations.WriteRawImage(decTextureData, width, height, outputStream, exportType))
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: failed to write image to disk");
                }
            }
        }

        if (emptyTextureCount == selection.Count)
        {
            await funcs.ShowMessageDialog("Error", "All textures are empty. No textures were exported.");
            return false;
        }

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Error", firstLinesStr);
        }

        return true;
    }

    public Task<bool> SingleExport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        AssetInst asset = selection[0];
        if (asset.Type == AssetClassID.Texture2D)
            return SingleExportTexture2D(workspace, funcs, asset);
        else if (asset.Type == AssetClassID.Sprite)
            return SingleExportTextureSprite(workspace, funcs, asset);
        else
            return Task.FromResult(false);
    }

    private async Task<bool> SingleExportTexture2D(Workspace workspace, IUavPluginFunctions funcs, AssetInst asset)
    {
        AssetTypeValueField? texBaseField = TextureHelper.GetByteArrayTexture(workspace, asset);
        TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

        TextureHelper.SwizzleOptIn(texFile, asset.FileInstance.file);

        // 0x0 texture, usually called like Font Texture or something
        if (texFile.m_Width == 0 && texFile.m_Height == 0)
        {
            await funcs.ShowMessageDialog("Error", "Texture size is 0x0 which is not exportable.");
            return false;
        }

        string assetName = PathUtils.ReplaceInvalidPathChars(asset.AssetName ?? "Texture2D");
        var filePath = await ShowImageSaveFileDialog(funcs, asset, assetName);
        if (filePath == null)
        {
            return false;
        }

        ImageExportType exportType = ExportTypeFromFileName(filePath);

        using FileStream outputStream = File.OpenWrite(filePath);
        byte[] encTextureData = texFile.FillPictureData(asset.FileInstance);
        if (!texFile.DecodeTextureImage(encTextureData, outputStream, exportType))
        {
            string errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to decode (missing resS, invalid texture format, etc.)");
            return false;
        }

        return true;
    }

    private async Task<bool> SingleExportTextureSprite(Workspace workspace, IUavPluginFunctions funcs, AssetInst asset)
    {
        string assetName = PathUtils.ReplaceInvalidPathChars(asset.AssetName ?? "Sprite");
        var filePath = await ShowImageSaveFileDialog(funcs, asset, assetName);
        if (filePath == null)
        {
            return false;
        }

        ImageExportType exportType = ExportTypeFromFileName(filePath);
        string errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";

        TextureLoader texLoader = new TextureLoader();
        byte[]? decTextureData = texLoader.GetSpriteRawBytes(workspace, asset, out var _, out var width, out var height);
        if (decTextureData == null)
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to decode (missing resS, invalid texture format, invalid sprite, etc.)");
            return false;
        }

        // SKBitmap is RGBA32 but StbIws expects BGRA32. swap R and B.
        TextureOperations.SwapRBComponents(decTextureData);

        // image is also upside down. flip it (normally assetstools.net.texture handles this)
        TextureOperations.FlipBGRA32Vertically(decTextureData, width, height);

        using FileStream outputStream = File.OpenWrite(filePath);
        if (!TextureOperations.WriteRawImage(decTextureData, width, height, outputStream, exportType))
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to write image to disk");
            return false;
        }

        return true;
    }

    private static Task<string?> ShowImageSaveFileDialog(IUavPluginFunctions funcs, AssetInst asset, string assetName)
    {
        return funcs.ShowSaveFileDialog(new FilePickerSaveOptions()
        {
            Title = "Save texture",
            FileTypeChoices =
            [
                new("PNG file") { Patterns = ["*.png"] },
                new("BMP file") { Patterns = ["*.bmp"] },
                new("JPG file") { Patterns = ["*.jpg", "*.jpeg"] },
                new("TGA file") { Patterns = ["*.tga"] },
            ],
            SuggestedFileName = AssetNameUtils.GetAssetFileName(asset, assetName, string.Empty),
            DefaultExtension = "png"
        });
    }

    private static ImageExportType ExportTypeFromFileName(string fileName)
    {
        return Path.GetExtension(fileName) switch
        {
            ".bmp" => ImageExportType.Bmp,
            ".png" => ImageExportType.Png,
            ".jpg" or ".jpeg" => ImageExportType.Jpg,
            ".tga" => ImageExportType.Tga,
            _ => ImageExportType.Png
        };
    }
}
