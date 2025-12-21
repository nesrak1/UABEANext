using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace TextAssetPlugin;
public class ExportTextAssetPlugin : IUavPluginOption
{
    public string Name => "Export TextAsset";
    public string Description => "Exports TextAssets to txt";
    public UavPluginMode Options => UavPluginMode.Export;

    public bool SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Export)
        {
            return false;
        }

        var typeId = (int)AssetClassID.TextAsset;
        return selection.All(a => a.TypeId == typeId);
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
        var dir = await funcs.ShowOpenFolderDialog(new FolderPickerOpenOptions()
        {
            Title = "Select export directory"
        });

        if (dir == null)
        {
            return false;
        }

        var errorBuilder = new StringBuilder();
        foreach (var asset in selection)
        {
            var errorAssetName = $"{Path.GetFileName(asset.FileInstance.path)}/{asset.PathId}";
            var textBaseField = workspace.GetBaseField(asset);
            if (textBaseField == null)
            {
                errorBuilder.AppendLine($"[{errorAssetName}]: failed to read");
                continue;
            }

            var name = textBaseField["m_Name"].AsString;
            var byteData = textBaseField["m_Script"].AsByteArray;

            var assetName = PathUtils.ReplaceInvalidPathChars(name);
            var filePath = Path.Combine(dir, AssetNameUtils.GetAssetFileName(asset, assetName, ".txt"));

            File.WriteAllBytes(filePath, byteData);
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
        var asset = selection[0];
        var textBaseField = workspace.GetBaseField(asset);
        if (textBaseField == null)
        {
            await funcs.ShowMessageDialog("Error", "Failed to read");
            return false;
        }

        var name = textBaseField["m_Name"].AsString;
        var byteData = textBaseField["m_Script"].AsByteArray;

        string assetName = PathUtils.ReplaceInvalidPathChars(name);
        var filePath = await funcs.ShowSaveFileDialog(new FilePickerSaveOptions()
        {
            Title = "Save text asset",
            FileTypeChoices = new List<FilePickerFileType>()
            {
                new("TXT file (*.txt)") { Patterns = ["*.txt"] },
                new("BYTES file (*.bytes)") { Patterns = ["*.bytes"] },
                new("All types (*.*)") { Patterns = ["*"] },
            },
            SuggestedFileName = AssetNameUtils.GetAssetFileName(asset, assetName, string.Empty),
            DefaultExtension = "txt"
        });

        if (filePath == null)
        {
            return false;
        }

        File.WriteAllBytes(filePath, byteData);
        return true;
    }
}
