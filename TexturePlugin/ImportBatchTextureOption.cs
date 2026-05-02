using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform.Storage;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.ViewModels.Dialogs;
using UABEANext4.Util;

namespace TexturePlugin;

public class ImportBatchTextureOption : IUavPluginOption
{
    public string Name => LocalizationHelper.GetString("Plugins.Texture.Import", "Import Texture2D");

    public string Description => "Imports a folder of png/tga/bmp/jpgs into Texture2Ds";

    public UavPluginMode Options => UavPluginMode.Import;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Import)
        {
            return false;
        }

        var typeId = (int)AssetClassID.Texture2D;
        return selection.All(a => a.TypeId == typeId);
    }

    public async Task<bool> Execute(Workspace workspace, IUavPluginFunctions funcs, UavPluginMode mode, IList<AssetInst> selection)
    {
        return await BatchImport(workspace, funcs, selection);
    }

    public async Task<bool> BatchImport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var dir = await funcs.ShowOpenFolderDialog(new FolderPickerOpenOptions()
        {
            Title = LocalizationHelper.GetString("Plugins.Texture.SelectExportDir", "Select import directory")
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

        var success = await ImportTextures(workspace, funcs, batchInfosResult);
        return success;
    }

    private async Task<bool> ImportTextures(Workspace workspace, IUavPluginFunctions funcs, List<ImportBatchInfo> infos)
    {
        var errorBuilder = new StringBuilder();
        var fileNamesToDirty = new HashSet<string>();
        var updatedAssets = new List<AssetInst>();
        int totalCount = infos.Count;
        int processed = 0;
        object lockObj = new object();

        workspace.SetProgressThreadSafe(0f, "Starting import...");

        await Task.Run(() =>
        {
            // Leave one core free for the UI thread to keep the app responsive
            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) 
            };

            Parallel.ForEach(infos, parallelOptions, info =>
            {
                var asset = info.Asset;
                var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";

                if (info.ImportFile == null || !File.Exists(info.ImportFile))
                {
                    lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to import because {info.ImportFile ?? "[null]"} does not exist.");
                    return;
                }

                AssetTypeValueField? baseField;
                // 1. Read metadata (locked)
                lock (asset.FileInstance.LockReader)
                {
                    baseField = workspace.GetBaseField(asset);
                }

                if (baseField == null)
                {
                    lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                    return;
                }

                // 2. Parse and encode (not locked - CPU intensive)
                var tex = TextureFile.ReadTextureFile(baseField);

                try
                {
                    // disable mips until we can support them
                    tex.m_MipCount = 1;
                    tex.m_MipMap = false;

                    // EncodeTextureImage reads the file and encodes it to the target format (slow, parallelizable)
                    tex.EncodeTextureImage(info.ImportFile);

                    // Writing back to the base field and byte array
                    tex.WriteTo(baseField);
                    
                    // 3. Update asset data (silent, not locked as it only affects this asset)
                    asset.UpdateAssetDataSilent(workspace, baseField);

                    lock (lockObj)
                    {
                        updatedAssets.Add(asset);
                        fileNamesToDirty.Add(asset.FileInstance.name);
                    }
                }
                catch (Exception e)
                {
                    lock (lockObj) errorBuilder.AppendLine($"[{errorAssetName}]: failed to import: {e}");
                }

                int curr = Interlocked.Increment(ref processed);
                if (curr % 20 == 0 || curr == totalCount)
                {
                    workspace.SetProgressThreadSafe((float)curr / totalCount, $"Importing {curr:N0}/{totalCount:N0}...");
                }
            });
        });

        // Batch refresh: fire PropertyChanged for all updated assets at once
        foreach (var asset in updatedAssets)
        {
            asset.RefreshRow();
        }

        // Batch dirty: mark files as dirty only once per file, not per asset
        foreach (var fileName in fileNamesToDirty)
        {
            if (workspace.ItemLookup.TryGetValue(fileName, out var fileToDirty))
            {
                workspace.Dirty(fileToDirty);
            }
        }

        workspace.SetProgressThreadSafe(0f, "");

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Error", firstLinesStr);
        }

        return true;
    }
}
