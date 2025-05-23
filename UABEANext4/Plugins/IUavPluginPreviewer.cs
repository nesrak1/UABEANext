using Avalonia.Media.Imaging;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Plugins;
public interface IUavPluginPreviewer
{
    string Name { get; }
    string Description { get; }

    // this only supports one previewer per plugin, but that's probably fine for now
    UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection);
    string? ExecuteText(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error);
    Bitmap? ExecuteImage(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error);
    MeshObj? ExecuteMesh(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection, out string? error);

    // called when the workspace is closing/resetting
    void Cleanup();
}