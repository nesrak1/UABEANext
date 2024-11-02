using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform.Storage;
using System.Text;
using TexturePlugin.ViewModels;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace TexturePlugin;

public class ExportTextureOption : IUavPluginOption
{
    public string Name => "Export Texture2D";

    public string Description => "Exports Texture2Ds to png/tga/bmp/jpg";

    public UavPluginMode Options => UavPluginMode.Export;

    public Task<bool> SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Export)
        {
            return Task.FromResult(false);
        }

        var typeId = (int)AssetClassID.Texture2D;
        return Task.FromResult(selection.All(a => a.TypeId == typeId));
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

        StringBuilder errorBuilder = new StringBuilder();
        int emptyTextureCount = 0;
        foreach (AssetInst asset in selection)
        {
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
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

            string assetName = PathUtils.ReplaceInvalidPathChars(texFile.m_Name);
            string filePath = AssetNameUtils.GetAssetFileName(asset, assetName, fileExtension);

            using FileStream outputStream = File.OpenWrite(filePath);
            byte[] encTextureData = texFile.FillPictureData(asset.FileInstance);
            bool success = texFile.DecodeTextureImage(encTextureData, outputStream, exportType);
            if (!success)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to decode (missing resS, invalid texture format, etc.)");
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

    public async Task<bool> SingleExport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        AssetInst asset = selection[0];

        AssetTypeValueField? texBaseField = TextureHelper.GetByteArrayTexture(workspace, asset);
        TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);

        TextureHelper.SwizzleOptIn(texFile, asset.FileInstance.file);

        // 0x0 texture, usually called like Font Texture or something
        if (texFile.m_Width == 0 && texFile.m_Height == 0)
        {
            await funcs.ShowMessageDialog("Error", "Texture size is 0x0 which is not exportable.");
            return false;
        }

        string assetName = PathUtils.ReplaceInvalidPathChars(texFile.m_Name);
        var filePath = await funcs.ShowSaveFileDialog(new FilePickerSaveOptions()
        {
            Title = "Save texture",
            FileTypeChoices = new List<FilePickerFileType>()
            {
                new FilePickerFileType("PNG file") { Patterns = new List<string>() { "*.png" } },
                new FilePickerFileType("BMP file") { Patterns = new List<string>() { "*.bmp" } },
                new FilePickerFileType("JPG file") { Patterns = new List<string>() { "*.jpg", "*.jpeg" } },
                new FilePickerFileType("TGA file") { Patterns = new List<string>() { "*.tga" } },
            },
            SuggestedFileName = AssetNameUtils.GetAssetFileName(asset, assetName, string.Empty),
            DefaultExtension = "png"
        });

        if (filePath == null)
        {
            return false;
        }

        ImageExportType exportType = Path.GetExtension(filePath) switch
        {
            ".bmp" => ImageExportType.Bmp,
            ".png" => ImageExportType.Png,
            ".jpg" or ".jpeg" => ImageExportType.Jpg,
            ".tga" => ImageExportType.Tga,
            _ => ImageExportType.Png
        };

        using FileStream outputStream = File.OpenWrite(filePath);
        byte[] encTextureData = texFile.FillPictureData(asset.FileInstance);
        bool success = texFile.DecodeTextureImage(encTextureData, outputStream, exportType);
        if (!success)
        {
            string errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to decode (missing resS, invalid texture format, etc.)");
        }

        return success;
    }
}
