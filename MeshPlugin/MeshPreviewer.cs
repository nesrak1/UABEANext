using AssetsTools.NET.Extra;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;
using UABEANext4.Plugins;

namespace MeshPlugin;
public class MeshPreviewer : IUavPluginPreviewer
{
    public string Name => "Preview Mesh";
    public string Description => "Preview Meshes";

    private static bool IsGameObjectWithMeshFilter(Workspace workspace, AssetInst goAsset)
    {
        if (goAsset.Type != AssetClassID.GameObject)
            return false;

        var goBase = workspace.GetBaseField(goAsset);
        if (goBase is null)
            return false;

        var goComponents = goBase["m_Component.Array"];
        foreach (var componentPair in goComponents)
        {
            var component = componentPair[componentPair.Children.Count - 1];
            // cheaper to use AssetFileInfo rather than AssetInst
            var componentInf = workspace.GetAssetFileInfo(goAsset.FileInstance, component);
            if (componentInf is not null && componentInf.TypeId == (int)AssetClassID.MeshFilter)
            {
                return true;
            }
        }

        return false;
    }

    private static AssetInst? GetMeshFromGameObject(Workspace workspace, AssetInst goAsset)
    {
        var goBase = workspace.GetBaseField(goAsset);
        if (goBase is null)
            return null;

        var goComponents = goBase["m_Component.Array"];
        foreach (var componentPair in goComponents)
        {
            var component = componentPair[componentPair.Children.Count - 1];
            // cheaper to use AssetFileInfo rather than AssetInst
            var componentInf = workspace.GetAssetFileInfo(goAsset.FileInstance, component);
            if (componentInf is not null && componentInf.TypeId == (int)AssetClassID.MeshFilter)
            {
                var mfiltAsset = new AssetInst(goAsset.FileInstance, componentInf);
                var mfiltBase = workspace.GetBaseField(mfiltAsset);
                if (mfiltBase is null)
                    return null;

                var meshAsset = workspace.GetAssetInst(mfiltAsset.FileInstance, mfiltBase["m_Mesh"]);
                if (meshAsset is null)
                    return null;

                return meshAsset;
            }
        }

        return null;
    }

    public UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection)
    {
        var previewType = selection.Type == AssetClassID.Mesh || IsGameObjectWithMeshFilter(workspace, selection)
            ? UavPluginPreviewerType.Mesh
            : UavPluginPreviewerType.None;

        return previewType;
    }

    public PreviewResult Execute(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection)
    {
        try
        {
            // if we selected a gameobject, do gameobject -> meshfilter -> mesh
            if (selection.Type == AssetClassID.GameObject)
            {
                // todo: make GetComponent helper function for all plugins
                var maybeMeshAsset = GetMeshFromGameObject(workspace, selection);
                if (maybeMeshAsset is null)
                {
                    return new PreviewResult.Error("No preview available (mesh couldn't be loaded).");
                }

                selection = maybeMeshAsset;
            }

            var meshBf = workspace.GetBaseField(selection);
            if (meshBf == null)
            {
                return new PreviewResult.Error("No preview available (mesh base field couldn't be loaded).");
            }

            var version = new UnityVersion(selection.FileInstance.file.Metadata.UnityVersion);
            var meshObj = new MeshObj(selection.FileInstance, meshBf, version);

            return new PreviewResult.Mesh(meshObj);

        }
        catch (Exception ex)
        {
            string error = $"Mesh failed to decode due to an error. Exception:\n{ex}";
            return new PreviewResult.Error(error);
        }
    }

    public void Cleanup() { }
}
