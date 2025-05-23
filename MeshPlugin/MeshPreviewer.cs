using AssetsTools.NET.Extra;
using Avalonia.Media.Imaging;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;
using UABEANext4.Plugins;

namespace MeshPlugin;
public class MeshPreviewer : IUavPluginPreviewer
{
    public string Name => "Preview Mesh";
    public string Description => "Preview Meshes";

    public UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection)
    {
        var previewType = selection.Type == AssetClassID.Mesh
            ? UavPluginPreviewerType.Mesh
            : UavPluginPreviewerType.None;

        return previewType;
    }

    public MeshObj? ExecuteMesh(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
    {
        var meshBf = workspace.GetBaseField(selection);
        if (meshBf == null)
        {
            error = "No preview available.";
            return null;
        }

        var version = new UnityVersion(selection.FileInstance.file.Metadata.UnityVersion);
        var meshObj = new MeshObj(selection.FileInstance, meshBf, version);

        error = null;
        return meshObj;
    }

    public Bitmap? ExecuteImage(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
        => throw new InvalidOperationException();

    public string? ExecuteText(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error)
        => throw new InvalidOperationException();

    public void Cleanup() { }
}
