using UABEANext4.AssetWorkspace;

namespace UABEANext4.Plugins;
public interface IUavPluginPreviewer
{
    string Name { get; }
    string Description { get; }

    // this only supports one previewer per plugin, but that's probably fine for now
    UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection);
    PreviewResult Execute(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection);

    // called when the workspace is closing/resetting
    void Cleanup();
}