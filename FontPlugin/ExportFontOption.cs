using AssetsTools.NET.Extra;
using Avalonia.Platform.Storage;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;
using UABEANext4.Util;

namespace FontPlugin;

public class ExportFontOption : IUavPluginOption
{
    public string Name => "Export Font";

    public string Description => "Exports Fonts to ttf/otf";

    public UavPluginMode Options => UavPluginMode.Export;

    public Task<bool> SupportsSelection(Workspace workspace, UavPluginMode mode, IList<AssetInst> selection)
    {
        if (mode != UavPluginMode.Export)
        {
            return Task.FromResult(false);
        }

        var typeId = (int)AssetClassID.Font;
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

            var isOtf = FontHelper.IsDataOtf(byteData);
            var extension = isOtf ? ".otf" : ".ttf";

            var assetName = PathUtils.ReplaceInvalidPathChars(name);
            var filePath = AssetNameUtils.GetAssetFileName(asset, assetName, extension);

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
            await funcs.ShowMessageDialog("Error", "Failed to read Font");
            return false;
        }

        var name = textBaseField["m_Name"].AsString;
        var byteData = textBaseField["m_Script"].AsByteArray;

        var isOtf = FontHelper.IsDataOtf(byteData);
        var extension = isOtf ? "otf" : "ttf";

        string assetName = PathUtils.ReplaceInvalidPathChars(name);
        var filePath = await funcs.ShowSaveFileDialog(new FilePickerSaveOptions()
        {
            Title = "Save font",
            FileTypeChoices = new List<FilePickerFileType>()
            {
                new FilePickerFileType($"{extension.ToUpper()} file (*.{extension})") { Patterns = new List<string>() { "*." + extension } },
            },
            SuggestedFileName = AssetNameUtils.GetAssetFileName(asset, assetName, string.Empty),
            DefaultExtension = "png"
        });

        if (filePath == null)
        {
            return false;
        }

        File.WriteAllBytes(filePath, byteData);
        return true;
    }
}
