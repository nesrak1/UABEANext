using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform.Storage;
using System.Text;
using TexturePlugin.Helpers;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.ViewModels.Dialogs;

namespace TexturePlugin;

public class ImportSpriteOption : IUavPluginOption
{
    public string Name => "Import Sprite";
    public string Description => "Imports png/tga/bmp/jpgs into Sprites (replaces underlying Texture2D)";
    public UavPluginMode Options => UavPluginMode.Import;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Import)
        {
            return false;
        }

        var typeId = (int)AssetClassID.Sprite;
        return selection.All(a => a.TypeId == typeId);
    }

    public async Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (selection.Count > 1)
        {
            return await BatchImport(workspace, funcs, selection);
        }
        else
        {
            return await SingleImport(workspace, funcs, selection);
        }
    }

    private async Task<bool> SingleImport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var asset = selection[0];

        var filePaths = await funcs.ShowOpenFileDialog(new FilePickerOpenOptions()
        {
            Title = "Select image file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new("Image files") { Patterns = ["*.png", "*.bmp", "*.jpg", "*.jpeg", "*.tga"] }
            ]
        });

        if (filePaths == null || filePaths.Length == 0)
        {
            return false;
        }

        var success = await ImportSprite(workspace, funcs, asset, filePaths[0]);
        return success;
    }

    private async Task<bool> BatchImport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var dir = await funcs.ShowOpenFolderDialog(new FolderPickerOpenOptions()
        {
            Title = "Select import directory"
        });

        if (dir == null)
        {
            return false;
        }

        var extensions = new List<string>() { "bmp", "png", "jpg", "jpeg", "tga" };
        var batchInfosViewModel = new BatchImportViewModel(workspace, selection.ToList(), dir, extensions);

        if (batchInfosViewModel.DataGridItems.Count == 0)
        {
            await funcs.ShowMessageDialog("Error", "No matching files found in the directory. Make sure the file names are in UABEA's format.");
            return false;
        }

        var batchInfosResult = await funcs.ShowDialog(batchInfosViewModel);
        if (batchInfosResult == null)
        {
            return false;
        }

        var success = await ImportSprites(workspace, funcs, batchInfosResult);
        return success;
    }

    private async Task<bool> ImportSprites(Workspace workspace, IUavPluginFunctions funcs, List<ImportBatchInfo> infos)
    {
        var errorBuilder = new StringBuilder();
        int successCount = 0;

        foreach (var info in infos)
        {
            var asset = info.Asset;
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";

            if (info.ImportFile == null || !File.Exists(info.ImportFile))
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to import because {info.ImportFile ?? "[null]"} does not exist.");
                continue;
            }

            try
            {
                bool success = await ImportSprite(workspace, funcs, asset, info.ImportFile);
                if (success)
                {
                    successCount++;
                }
                else
                {
                    errorBuilder.AppendLine($"[{errorAssetName}]: import returned false");
                }
            }
            catch (Exception e)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to import: {e.Message}");
            }
        }

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Import Results", $"Successfully imported {successCount}/{infos.Count} sprites.\n\nErrors:\n{firstLinesStr}");
            return successCount > 0;
        }

        await funcs.ShowMessageDialog("Success", $"Successfully imported {successCount} sprite(s).");
        return true;
    }

    private async Task<bool> ImportSprite(Workspace workspace, IUavPluginFunctions funcs, AssetInst spriteAsset, string importFilePath)
    {
        var errorAssetName = $"{Path.GetFileName(spriteAsset.FileInstance.path)}/{spriteAsset.PathId}";

        // Validate import file
        if (!File.Exists(importFilePath))
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: Import file does not exist: {importFilePath}");
            return false;
        }

        var spriteBf = workspace.GetBaseField(spriteAsset);
        if (spriteBf == null)
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to read sprite data");
            return false;
        }

        var renderData = spriteBf["m_RD"];
        var texturePtr = AssetPPtr.FromField(renderData["texture"]);

        var spriteAtlas = spriteBf["m_SpriteAtlas"];
        var spriteAtlasPtr = AssetPPtr.FromField(spriteAtlas);

        if (!spriteAtlasPtr.IsNull())
        {
            await funcs.ShowMessageDialog("Warning",
                $"[{errorAssetName}]: This sprite uses a SpriteAtlas. " +
                "Importing will modify the atlas texture, which may affect other sprites. " +
                "Consider importing the Texture2D directly instead.");
        }

        if (texturePtr.IsNull())
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: sprite has no texture reference");
            return false;
        }

        var textureAsset = workspace.GetAssetInst(spriteAsset.FileInstance, texturePtr.FileId, texturePtr.PathId);
        if (textureAsset == null)
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to find texture asset");
            return false;
        }

        var texBaseField = TextureHelper.GetByteArrayTexture(workspace, textureAsset);
        if (texBaseField == null)
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to read texture");
            return false;
        }

        var tex = TextureFile.ReadTextureFile(texBaseField);

        // Disable mipmaps (matching ImportBatchTextureOption behavior)
        tex.m_MipCount = 1;
        tex.m_MipMap = false;

        try
        {
            // Encode the new texture
            tex.EncodeTextureImage(importFilePath);

            // Write changes back to the texture field
            tex.WriteTo(texBaseField);

            // CRITICAL: Update the texture asset
            textureAsset.UpdateAssetDataAndRow(workspace, texBaseField);

            // CRITICAL: Also update the sprite asset to mark it as modified
            // Even though we didn't change sprite data, we need to mark it as changed
            spriteAsset.UpdateAssetDataAndRow(workspace, spriteBf);

            return true;
        }
        catch (Exception e)
        {
            await funcs.ShowMessageDialog("Error", $"[{errorAssetName}]: failed to encode texture: {e.Message}\n\nStack trace:\n{e.StackTrace}");
            return false;
        }
    }
}