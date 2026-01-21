using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.ViewModels.Dialogs;

namespace FontPlugin;

public class ImportFontOption : IUavPluginOption
{
    public string Name => "Import Font";
    public string Description => "Imports ttf/otf files into Font assets";
    public UavPluginMode Options => UavPluginMode.Import;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Import)
        {
            return false;
        }

        var typeId = (int)AssetClassID.Font;
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

        var extensions = new List<string>() { "ttf", "otf" };
        var batchInfosViewModel = new BatchImportViewModel(workspace, selection.ToList(), dir, extensions);

        if (batchInfosViewModel.DataGridItems.Count == 0)
        {
            await funcs.ShowMessageDialog("Error", "No matching .ttf or .otf files found. Make sure the file names match the asset names.");
            return false;
        }

        var batchInfosResult = await funcs.ShowDialog(batchInfosViewModel);
        if (batchInfosResult == null)
        {
            return false;
        }

        var errorBuilder = new StringBuilder();
        foreach (ImportBatchInfo info in batchInfosResult)
        {
            var asset = info.Asset;
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";

            var baseField = FontHelper.GetByteArrayFont(workspace, asset);
            if (baseField == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to read font template");
                continue;
            }

            var filePath = info.ImportFile;
            if (filePath == null || !File.Exists(filePath))
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: file {info.ImportFile ?? "[null]"} does not exist.");
                continue;
            }

            try
            {
                byte[] byteData = File.ReadAllBytes(filePath);
                baseField["m_FontData.Array"].AsByteArray = byteData;
                asset.UpdateAssetDataAndRow(workspace, baseField);
            }
            catch (Exception ex)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: {ex.Message}");
            }
        }

        if (errorBuilder.Length > 0)
        {
            string[] firstLines = errorBuilder.ToString().Split('\n').Take(20).ToArray();
            string firstLinesStr = string.Join('\n', firstLines);
            await funcs.ShowMessageDialog("Error", "Some errors occurred during import:\n" + firstLinesStr);
        }

        return true;
    }

    public async Task<bool> SingleImport(Workspace workspace, IUavPluginFunctions funcs, IList<AssetInst> selection)
    {
        var filePaths = await funcs.ShowOpenFileDialog(new FilePickerOpenOptions()
        {
            Title = "Load font file",
            FileTypeFilter = new List<FilePickerFileType>()
            {
                new("Font files (*.ttf, *.otf)") { Patterns = ["*.ttf", "*.otf"] },
                new("All types (*.*)") { Patterns = ["*"] },
            },
            AllowMultiple = false
        });

        if (filePaths == null || filePaths.Length == 0)
        {
            return false;
        }

        var filePath = filePaths[0];
        if (!File.Exists(filePath))
        {
            await funcs.ShowMessageDialog("Error", $"File {filePath} does not exist.");
            return false;
        }

        var asset = selection[0];
        var baseField = FontHelper.GetByteArrayFont(workspace, asset);
        if (baseField == null)
        {
            await funcs.ShowMessageDialog("Error", "Failed to read font asset structure.");
            return false;
        }

        try
        {
            byte[] byteData = File.ReadAllBytes(filePath);
            baseField["m_FontData.Array"].AsByteArray = byteData;
            asset.UpdateAssetDataAndRow(workspace, baseField);
        }
        catch (Exception ex)
        {
            await funcs.ShowMessageDialog("Error", $"Failed to import: {ex.Message}");
            return false;
        }

        return true;
    }
}