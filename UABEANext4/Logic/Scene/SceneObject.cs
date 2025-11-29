using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.Numerics;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Logic.Scene;

/// <summary>
/// Represents a single object in the scene view with transform, mesh, and material data.
/// </summary>
public class SceneObject
{
    public string Name { get; set; } = string.Empty;
    public long PathId { get; set; }
    public AssetInst? GameObjectAsset { get; set; }

    // Transform data
    public Vector3 LocalPosition { get; set; } = Vector3.Zero;
    public Quaternion LocalRotation { get; set; } = Quaternion.Identity;
    public Vector3 LocalScale { get; set; } = Vector3.One;

    // World transform (computed from hierarchy)
    public Matrix4x4 WorldMatrix { get; set; } = Matrix4x4.Identity;

    // Mesh data (if this object has a MeshFilter/MeshRenderer)
    public MeshObj? Mesh { get; set; }
    public bool HasMesh => Mesh != null && Mesh.Vertices.Length > 0;

    // Texture data (from material's main texture)
    public byte[]? TextureData { get; set; }
    public int TextureWidth { get; set; }
    public int TextureHeight { get; set; }
    public bool HasTexture => TextureData != null && TextureData.Length > 0;

    // UV data for texture mapping
    public float[]? UVs { get; set; }

    // Hierarchy
    public SceneObject? Parent { get; set; }
    public List<SceneObject> Children { get; } = new();

    // Selection state
    public bool IsSelected { get; set; }

    // Bounding box for picking
    public Vector3 BoundsMin { get; set; }
    public Vector3 BoundsMax { get; set; }

    public void ComputeWorldMatrix()
    {
        var localMatrix = Matrix4x4.CreateScale(LocalScale) *
                         Matrix4x4.CreateFromQuaternion(LocalRotation) *
                         Matrix4x4.CreateTranslation(LocalPosition);

        if (Parent != null)
        {
            WorldMatrix = localMatrix * Parent.WorldMatrix;
        }
        else
        {
            WorldMatrix = localMatrix;
        }

        // Recursively update children
        foreach (var child in Children)
        {
            child.ComputeWorldMatrix();
        }
    }

    public void ComputeBounds()
    {
        if (!HasMesh || Mesh == null)
        {
            BoundsMin = Vector3.Zero;
            BoundsMax = Vector3.Zero;
            return;
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < Mesh.Vertices.Length; i += 3)
        {
            var vertex = new Vector3(Mesh.Vertices[i], Mesh.Vertices[i + 1], Mesh.Vertices[i + 2]);
            var worldVertex = Vector3.Transform(vertex, WorldMatrix);

            min = Vector3.Min(min, worldVertex);
            max = Vector3.Max(max, worldVertex);
        }

        BoundsMin = min;
        BoundsMax = max;
    }

    public bool RayIntersects(Vector3 rayOrigin, Vector3 rayDirection, out float distance)
    {
        distance = float.MaxValue;

        if (!HasMesh)
            return false;

        // Simple AABB intersection test
        var invDir = new Vector3(
            rayDirection.X != 0 ? 1f / rayDirection.X : float.MaxValue,
            rayDirection.Y != 0 ? 1f / rayDirection.Y : float.MaxValue,
            rayDirection.Z != 0 ? 1f / rayDirection.Z : float.MaxValue
        );

        var t1 = (BoundsMin.X - rayOrigin.X) * invDir.X;
        var t2 = (BoundsMax.X - rayOrigin.X) * invDir.X;
        var t3 = (BoundsMin.Y - rayOrigin.Y) * invDir.Y;
        var t4 = (BoundsMax.Y - rayOrigin.Y) * invDir.Y;
        var t5 = (BoundsMin.Z - rayOrigin.Z) * invDir.Z;
        var t6 = (BoundsMax.Z - rayOrigin.Z) * invDir.Z;

        var tmin = MathF.Max(MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4)), MathF.Min(t5, t6));
        var tmax = MathF.Min(MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4)), MathF.Max(t5, t6));

        if (tmax < 0 || tmin > tmax)
            return false;

        distance = tmin >= 0 ? tmin : tmax;
        return true;
    }
}
