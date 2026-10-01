using Avalonia.Media.Imaging;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Plugins;

public interface IUavRawTexturePreviewer
{
    bool SupportsRawPreview(Workspace workspace, AssetInst asset);
    (Bitmap?, int) ExecuteImage(Workspace workspace, IUavPluginFunctions funcs,
        AssetInst asset, bool showSwizzled, out string? error);
}
