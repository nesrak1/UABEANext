using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Logic.Scene;

/// <summary>
/// Loads and manages scene data from Unity asset files.
/// </summary>
public class SceneData
{
    public List<SceneObject> RootObjects { get; } = new();
    public List<SceneObject> AllObjects { get; } = new();

    private readonly Workspace _workspace;
    private readonly Dictionary<long, SceneObject> _pathIdToObject = new();

    public SceneData(Workspace workspace)
    {
        _workspace = workspace;
    }

    public void LoadFromFile(AssetsFileInstance fileInst)
    {
        RootObjects.Clear();
        AllObjects.Clear();
        _pathIdToObject.Clear();

        // First pass: Create all scene objects with transforms
        var transformInfos = fileInst.file.GetAssetsOfType(AssetClassID.Transform)
            .Concat(fileInst.file.GetAssetsOfType(AssetClassID.RectTransform))
            .ToList();

        var goInfos = fileInst.file.GetAssetsOfType(AssetClassID.GameObject).ToList();
        var goPathIdToInfo = goInfos.ToDictionary(g => g.PathId);

        // Map transform PathId to its parent PathId
        var tfmParentMap = new Dictionary<long, long>();
        var tfmToGoMap = new Dictionary<long, long>();
        var tfmChildrenMap = new Dictionary<long, List<long>>();

        foreach (var tfmInfo in transformInfos)
        {
            var tfmBf = _workspace.GetBaseField(fileInst, tfmInfo.PathId);
            if (tfmBf == null) continue;

            var parentPathId = tfmBf["m_Father"]["m_PathID"].AsLong;
            tfmParentMap[tfmInfo.PathId] = parentPathId;

            var goPtr = tfmBf["m_GameObject"];
            var goPathId = goPtr["m_PathID"].AsLong;
            tfmToGoMap[tfmInfo.PathId] = goPathId;

            // Read transform data
            var localPos = ReadVector3(tfmBf["m_LocalPosition"]);
            var localRot = ReadQuaternion(tfmBf["m_LocalRotation"]);
            var localScale = ReadVector3(tfmBf["m_LocalScale"]);

            string goName = "[Unknown]";
            AssetInst? goAsset = null;
            if (goPathIdToInfo.TryGetValue(goPathId, out var goInfo))
            {
                goAsset = _workspace.GetAssetInst(fileInst, 0, goInfo.PathId);
                var goBf = _workspace.GetBaseField(fileInst, goInfo.PathId);
                if (goBf != null)
                {
                    goName = goBf["m_Name"].AsString;
                }
            }

            var sceneObj = new SceneObject
            {
                Name = goName,
                PathId = tfmInfo.PathId,
                GameObjectAsset = goAsset,
                LocalPosition = localPos,
                LocalRotation = localRot,
                LocalScale = localScale
            };

            _pathIdToObject[tfmInfo.PathId] = sceneObj;
            AllObjects.Add(sceneObj);

            // Track children
            var childrenArr = tfmBf["m_Children.Array"];
            var childIds = new List<long>();
            foreach (var child in childrenArr)
            {
                childIds.Add(child["m_PathID"].AsLong);
            }
            tfmChildrenMap[tfmInfo.PathId] = childIds;
        }

        // Second pass: Build hierarchy
        foreach (var kvp in _pathIdToObject)
        {
            var tfmPathId = kvp.Key;
            var sceneObj = kvp.Value;

            if (tfmParentMap.TryGetValue(tfmPathId, out var parentPathId) && parentPathId != 0)
            {
                if (_pathIdToObject.TryGetValue(parentPathId, out var parentObj))
                {
                    sceneObj.Parent = parentObj;
                    parentObj.Children.Add(sceneObj);
                }
            }
            else
            {
                RootObjects.Add(sceneObj);
            }
        }

        // Third pass: Load meshes and materials for objects with MeshFilter
        var meshFilterInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshFilter).ToList();
        var meshRendererInfos = fileInst.file.GetAssetsOfType(AssetClassID.MeshRenderer).ToList();

        // Map GameObject PathId to MeshFilter/MeshRenderer
        var goToMeshFilter = new Dictionary<long, AssetFileInfo>();
        var goToMeshRenderer = new Dictionary<long, AssetFileInfo>();

        foreach (var mfInfo in meshFilterInfos)
        {
            var mfBf = _workspace.GetBaseField(fileInst, mfInfo.PathId);
            if (mfBf == null) continue;

            var goPathId = mfBf["m_GameObject"]["m_PathID"].AsLong;
            goToMeshFilter[goPathId] = mfInfo;
        }

        foreach (var mrInfo in meshRendererInfos)
        {
            var mrBf = _workspace.GetBaseField(fileInst, mrInfo.PathId);
            if (mrBf == null) continue;

            var goPathId = mrBf["m_GameObject"]["m_PathID"].AsLong;
            goToMeshRenderer[goPathId] = mrInfo;
        }

        // Load mesh data for each scene object
        foreach (var sceneObj in AllObjects)
        {
            var goPathId = tfmToGoMap.GetValueOrDefault(sceneObj.PathId, 0);
            if (goPathId == 0) continue;

            if (goToMeshFilter.TryGetValue(goPathId, out var mfInfo))
            {
                var mfBf = _workspace.GetBaseField(fileInst, mfInfo.PathId);
                if (mfBf == null) continue;

                var meshPtr = mfBf["m_Mesh"];
                var meshPathId = meshPtr["m_PathID"].AsLong;
                var meshFileId = meshPtr["m_FileID"].AsInt;

                if (meshPathId != 0)
                {
                    try
                    {
                        var meshBf = _workspace.GetBaseField(fileInst, meshFileId, meshPathId);
                        if (meshBf != null)
                        {
                            var version = fileInst.file.Metadata.UnityVersion;
                            sceneObj.Mesh = new MeshObj(fileInst, meshBf, new UnityVersion(version));

                            // Get UVs if available
                            if (sceneObj.Mesh.UVs != null && sceneObj.Mesh.UVs.Length > 0 && sceneObj.Mesh.UVs[0] != null)
                            {
                                sceneObj.UVs = sceneObj.Mesh.UVs[0];
                            }
                        }
                    }
                    catch
                    {
                        // Mesh loading failed, skip
                    }
                }
            }

            // Load texture from material
            if (goToMeshRenderer.TryGetValue(goPathId, out var mrInfo))
            {
                var mrBf = _workspace.GetBaseField(fileInst, mrInfo.PathId);
                if (mrBf == null) continue;

                var materialsArr = mrBf["m_Materials.Array"];
                if (materialsArr.Children.Count > 0)
                {
                    var matPtr = materialsArr[0];
                    var matPathId = matPtr["m_PathID"].AsLong;
                    var matFileId = matPtr["m_FileID"].AsInt;

                    if (matPathId != 0)
                    {
                        try
                        {
                            LoadTextureFromMaterial(fileInst, matFileId, matPathId, sceneObj);
                        }
                        catch
                        {
                            // Texture loading failed, skip
                        }
                    }
                }
            }
        }

        // Compute world matrices and bounds
        foreach (var root in RootObjects)
        {
            root.ComputeWorldMatrix();
        }

        foreach (var obj in AllObjects)
        {
            obj.ComputeBounds();
        }
    }

    private void LoadTextureFromMaterial(AssetsFileInstance fileInst, int matFileId, long matPathId, SceneObject sceneObj)
    {
        var matBf = _workspace.GetBaseField(fileInst, matFileId, matPathId);
        if (matBf == null) return;

        // Try to find _MainTex in the saved properties
        var texEnvs = matBf["m_SavedProperties"]["m_TexEnvs.Array"];
        foreach (var texEnv in texEnvs)
        {
            var texName = texEnv["first"].AsString;
            if (texName == "_MainTex" || texName == "_BaseMap" || texName == "_Albedo")
            {
                var texPtr = texEnv["second"]["m_Texture"];
                var texPathId = texPtr["m_PathID"].AsLong;
                var texFileId = texPtr["m_FileID"].AsInt;

                if (texPathId != 0)
                {
                    LoadTexture(fileInst, texFileId, texPathId, sceneObj);
                    return;
                }
            }
        }
    }

    private void LoadTexture(AssetsFileInstance fileInst, int texFileId, long texPathId, SceneObject sceneObj)
    {
        var texAsset = _workspace.GetAssetInst(fileInst, texFileId, texPathId);
        if (texAsset == null) return;

        var texBf = _workspace.GetBaseField(texAsset);
        if (texBf == null) return;

        try
        {
            var texture = TextureFile.ReadTextureFile(texBf);
            var encData = texture.FillPictureData(texAsset.FileInstance);
            var decData = texture.DecodeTextureRaw(encData);

            if (decData != null)
            {
                sceneObj.TextureData = decData;
                sceneObj.TextureWidth = texture.m_Width;
                sceneObj.TextureHeight = texture.m_Height;
            }
        }
        catch
        {
            // Texture decode failed
        }
    }

    private static Vector3 ReadVector3(AssetTypeValueField field)
    {
        return new Vector3(
            field["x"].AsFloat,
            field["y"].AsFloat,
            field["z"].AsFloat
        );
    }

    private static Quaternion ReadQuaternion(AssetTypeValueField field)
    {
        return new Quaternion(
            field["x"].AsFloat,
            field["y"].AsFloat,
            field["z"].AsFloat,
            field["w"].AsFloat
        );
    }

    public SceneObject? PickObject(Vector3 rayOrigin, Vector3 rayDirection)
    {
        SceneObject? closest = null;
        float closestDist = float.MaxValue;

        foreach (var obj in AllObjects)
        {
            if (obj.RayIntersects(rayOrigin, rayDirection, out float dist))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = obj;
                }
            }
        }

        return closest;
    }

    public void DeselectAll()
    {
        foreach (var obj in AllObjects)
        {
            obj.IsSelected = false;
        }
    }
}
