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
    public string Name => LocalizationHelper.GetString("Plugins.Texture.Export", "Export Texture2D/Sprite");
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
            Title = LocalizationHelper.GetString("Plugins.Texture.SelectExportDir", "Select export directory")
        });

        if (dir == null)
        {
            return false;
        }

        TextureLoader sharedTexLoader = new TextureLoader();
        StringBuilder errorBuilder = new StringBuilder();
        int emptyTextureCount = 0;
        int processed = 0;
        int totalCount = selection.Count;
        object lockObj = new object();

        workspace.SetProgressThreadSafe(0f, "Starting export...");

        await Task.Run(() =>
        {
            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) 
            };

            Parallel.ForEach(selection, parallelOptions, asset =>
            {
                var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
                try
                {
                    if (asset.Type == AssetClassID.Texture2D)
                    {
                        AssetTypeValueField? texBaseField;
                        // 1. Read metadata (locked)
                        lock (asset.FileInstance.LockReader)
                        {
                            texBaseField = TextureHelper.GetByteArrayTexture(workspace, asset);
                        }

                        if (texBaseField == null)
                        {
                            lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                            return;
                        }

                        // 2. Parse metadata (not locked)
                        TextureFile texFile = TextureFile.ReadTextureFile(texBaseField);
                        TextureHelper.SwizzleOptIn(texFile, asset.FileInstance.file);

                        if (texFile.m_Width == 0 && texFile.m_Height == 0)
                        {
                            Interlocked.Increment(ref emptyTextureCount);
                            return;
                        }

                        // 3. Read pixel data (locked)
                        byte[] encTextureData;
                        lock (asset.FileInstance.LockReader)
                        {
                            encTextureData = texFile.FillPictureData(asset.FileInstance);
                        }

                        if (encTextureData == null || encTextureData.Length == 0)
                        {
                            lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to find texture data (missing resS file?)");
                            return;
                        }

                        string assetName = PathUtils.ReplaceInvalidPathChars(asset.AssetName ?? "Texture2D");
                        string filePath = AssetNamer.GetAssetFileName(asset, assetName, fileExtension);

                        // 4. Decode and save to disk (not locked - CPU intensive)
                        using FileStream outputStream = File.OpenWrite(Path.Combine(dir, filePath));
                        bool success = texFile.DecodeTextureImage(encTextureData, outputStream, exportType);
                        if (!success)
                        {
                            lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to decode or write image to disk (invalid texture format, etc.)");
                        }
                    }
                    else if (asset.Type == AssetClassID.Sprite)
                    {
                        byte[]? decTextureData;
                        int width, height;

                        // Use shared thread-safe TextureLoader to benefit from cache
                        decTextureData = sharedTexLoader.GetSpriteRawBytes(workspace, asset, true, out var _, out width, out height);

                        if (decTextureData == null)
                        {
                            lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to decode (missing resS, invalid texture format, invalid sprite, etc.)");
                            return;
                        }

                        string assetName = PathUtils.ReplaceInvalidPathChars(asset.AssetName ?? "Sprite");
                        string filePath = AssetNamer.GetAssetFileName(asset, assetName, fileExtension);

                        TextureOperations.SwapRBComponents(decTextureData);
                        TextureOperations.FlipBGRA32Vertically(decTextureData, width, height);

                        using FileStream outputStream = File.OpenWrite(Path.Combine(dir, filePath));
                        if (!TextureOperations.WriteRawImage(decTextureData, width, height, outputStream, exportType))
                        {
                            lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to write image to disk");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: Exception: {ex.Message}");
                }

                int curr = Interlocked.Increment(ref processed);
                if (curr % 20 == 0 || curr == totalCount)
                {
                    workspace.SetProgressThreadSafe((float)curr / totalCount, $"Exporting {curr:N0}/{totalCount:N0}...");
                }
            });
        });

        workspace.SetProgressThreadSafe(0f, "");

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
        byte[]? decTextureData = texLoader.GetSpriteRawBytes(workspace, asset, true, out var _, out var width, out var height);
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
            Title = LocalizationHelper.GetString("Plugins.Texture.SaveTexture", "Save texture"),
            FileTypeChoices =
            [
                new("PNG file") { Patterns = ["*.png"] },
                new("BMP file") { Patterns = ["*.bmp"] },
                new("JPG file") { Patterns = ["*.jpg", "*.jpeg"] },
                new("TGA file") { Patterns = ["*.tga"] },
            ],
            SuggestedFileName = AssetNamer.GetAssetFileName(asset, assetName, string.Empty),
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
