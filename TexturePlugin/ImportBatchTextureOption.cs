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

    public Task<bool> SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Import)
        {
            return Task.FromResult(false);
        }

        var typeId = (int)AssetClassID.Texture2D;
        return Task.FromResult(selection.All(a => a.TypeId == typeId));
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

        var extensions = new List<string>() { "png", "tga", "bmp", "tga" };
        var batchInfos = await funcs.ShowDialog(new BatchImportViewModel(workspace, selection.ToList(), dir, extensions));
        if (batchInfos == null)
        {
            return false;
        }

        var success = await ImportTextures(workspace, funcs, batchInfos);
        return success;
    }

    private async Task<bool> ImportTextures(Workspace workspace, IUavPluginFunctions funcs, List<ImportBatchInfo> infos)
    {
        var errorBuilder = new StringBuilder();
        foreach (var info in infos)
        {
            var asset = info.Asset;
            var baseField = workspace.GetBaseField(asset);
            var tex = TextureFile.ReadTextureFile(baseField);

            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            if (info.ImportFile == null || !File.Exists(info.ImportFile))
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to import because {info.ImportFile ?? "[null]"} does not exist.");
            }

            try
            {
                tex.SetTextureData(info.ImportFile);
            }
            catch (Exception e)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to import: {e.Message}");
            }

            if (errorBuilder.Length > 0)
            {
                string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
                string firstLinesStr = string.Join('\n', firstLines);
                await funcs.ShowMessageDialog("Error", firstLinesStr);
            }
        }

        return true;
    }
}
