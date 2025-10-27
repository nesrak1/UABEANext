using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Platform.Storage;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.ViewModels.Dialogs;

namespace TexturePlugin;

public class ImportBatchTextureOption : IUavPluginOption
{
    public string Name => "Import Texture2D";

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

        var success = await ImportTextures(workspace, funcs, batchInfosResult);
        return success;
    }

    private async Task<bool> ImportTextures(Workspace workspace, IUavPluginFunctions funcs, List<ImportBatchInfo> infos)
    {
        var errorBuilder = new StringBuilder();
        foreach (var info in infos)
        {
            var asset = info.Asset;
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";

            var baseField = workspace.GetBaseField(asset);
            if (baseField == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                continue;
            }

            var tex = TextureFile.ReadTextureFile(baseField);
            if (info.ImportFile == null || !File.Exists(info.ImportFile))
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to import because {info.ImportFile ?? "[null]"} does not exist.");
                continue;
            }

            try
            {
                // disable mips until we can support them
                tex.m_MipCount = 1;
                tex.m_MipMap = false;

                tex.EncodeTextureImage(info.ImportFile);
                tex.WriteTo(baseField);
                asset.UpdateAssetDataAndRow(workspace, baseField);
            }
            catch (Exception e)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to import: {e}");
            }
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
