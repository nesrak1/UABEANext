using AssetsTools.NET.Extra;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UABEANext4.Converters;
public class AssetTypeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AssetClassID assetClass)
        {
            if ((int)assetClass < 0)
            {
                return GetBitmap("UABEANext4/Assets/Icons/asset-mono-behaviour.png");
            }

            return assetClass switch
            {
                AssetClassID.Animation => GetBitmap("UABEANext4/Assets/Icons/asset-animation.png"),
                AssetClassID.AnimationClip => GetBitmap("UABEANext4/Assets/Icons/asset-animation-clip.png"),
                AssetClassID.Animator => GetBitmap("UABEANext4/Assets/Icons/asset-animator.png"),
                AssetClassID.AnimatorController => GetBitmap("UABEANext4/Assets/Icons/asset-animator-controller.png"),
                AssetClassID.AnimatorOverrideController => GetBitmap("UABEANext4/Assets/Icons/asset-animator-override-controller.png"),
                AssetClassID.AudioClip => GetBitmap("UABEANext4/Assets/Icons/asset-audio-clip.png"),
                AssetClassID.AudioListener => GetBitmap("UABEANext4/Assets/Icons/asset-audio-listener.png"),
                AssetClassID.AudioMixer => GetBitmap("UABEANext4/Assets/Icons/asset-audio-mixer.png"),
                AssetClassID.AudioMixerGroup => GetBitmap("UABEANext4/Assets/Icons/asset-audio-mixer-group.png"),
                AssetClassID.AudioSource => GetBitmap("UABEANext4/Assets/Icons/asset-audio-source.png"),
                AssetClassID.Avatar => GetBitmap("UABEANext4/Assets/Icons/asset-avatar.png"),
                AssetClassID.BillboardAsset => GetBitmap("UABEANext4/Assets/Icons/asset-billboard.png"),
                AssetClassID.BillboardRenderer => GetBitmap("UABEANext4/Assets/Icons/asset-billboard-renderer.png"),
                AssetClassID.BoxCollider => GetBitmap("UABEANext4/Assets/Icons/asset-box-collider.png"),
                AssetClassID.Camera => GetBitmap("UABEANext4/Assets/Icons/asset-camera.png"),
                AssetClassID.Canvas => GetBitmap("UABEANext4/Assets/Icons/asset-canvas.png"),
                AssetClassID.CanvasGroup => GetBitmap("UABEANext4/Assets/Icons/asset-canvas-group.png"),
                AssetClassID.CanvasRenderer => GetBitmap("UABEANext4/Assets/Icons/asset-canvas-renderer.png"),
                AssetClassID.CapsuleCollider => GetBitmap("UABEANext4/Assets/Icons/asset-capsule-collider.png"),
                AssetClassID.CapsuleCollider2D => GetBitmap("UABEANext4/Assets/Icons/asset-capsule-collider.png"),
                AssetClassID.ComputeShader => GetBitmap("UABEANext4/Assets/Icons/asset-compute-shader.png"),
                AssetClassID.Cubemap => GetBitmap("UABEANext4/Assets/Icons/asset-cubemap.png"),
                AssetClassID.Flare => GetBitmap("UABEANext4/Assets/Icons/asset-flare.png"),
                AssetClassID.FlareLayer => GetBitmap("UABEANext4/Assets/Icons/asset-flare-layer.png"),
                AssetClassID.Font => GetBitmap("UABEANext4/Assets/Icons/asset-font.png"),
                AssetClassID.GameObject => GetBitmap("UABEANext4/Assets/Icons/asset-game-object.png"),
                AssetClassID.Light => GetBitmap("UABEANext4/Assets/Icons/asset-light.png"),
                AssetClassID.LightmapSettings => GetBitmap("UABEANext4/Assets/Icons/asset-lightmap-settings.png"),
                AssetClassID.LODGroup => GetBitmap("UABEANext4/Assets/Icons/asset-lod-group.png"),
                AssetClassID.Material => GetBitmap("UABEANext4/Assets/Icons/asset-material.png"),
                AssetClassID.Mesh => GetBitmap("UABEANext4/Assets/Icons/asset-mesh.png"),
                AssetClassID.MeshCollider => GetBitmap("UABEANext4/Assets/Icons/asset-mesh-collider.png"),
                AssetClassID.MeshFilter => GetBitmap("UABEANext4/Assets/Icons/asset-mesh-filter.png"),
                AssetClassID.MeshRenderer => GetBitmap("UABEANext4/Assets/Icons/asset-mesh-renderer.png"),
                AssetClassID.MonoBehaviour => GetBitmap("UABEANext4/Assets/Icons/asset-mono-behaviour.png"),
                AssetClassID.MonoScript => GetBitmap("UABEANext4/Assets/Icons/asset-mono-script.png"),
                AssetClassID.NavMeshSettings => GetBitmap("UABEANext4/Assets/Icons/asset-nav-mesh-settings.png"),
                AssetClassID.ParticleSystem => GetBitmap("UABEANext4/Assets/Icons/asset-particle-system.png"),
                AssetClassID.ParticleSystemRenderer => GetBitmap("UABEANext4/Assets/Icons/asset-particle-system-renderer.png"),
                AssetClassID.RectTransform => GetBitmap("UABEANext4/Assets/Icons/asset-rect-transform.png"),
                AssetClassID.ReflectionProbe => GetBitmap("UABEANext4/Assets/Icons/asset-reflection-probe.png"),
                AssetClassID.Rigidbody => GetBitmap("UABEANext4/Assets/Icons/asset-rigidbody.png"),
                AssetClassID.Shader => GetBitmap("UABEANext4/Assets/Icons/asset-shader.png"),
                AssetClassID.ShaderVariantCollection => GetBitmap("UABEANext4/Assets/Icons/asset-shader-collection.png"),
                AssetClassID.SkinnedMeshRenderer => GetBitmap("UABEANext4/Assets/Icons/asset-mesh-renderer.png"), // todo
                AssetClassID.Sprite => GetBitmap("UABEANext4/Assets/Icons/asset-sprite.png"),
                AssetClassID.SpriteRenderer => GetBitmap("UABEANext4/Assets/Icons/asset-sprite-renderer.png"),
                AssetClassID.Terrain => GetBitmap("UABEANext4/Assets/Icons/asset-terrain.png"),
                AssetClassID.TerrainCollider => GetBitmap("UABEANext4/Assets/Icons/asset-terrain-collider.png"),
                AssetClassID.TextAsset => GetBitmap("UABEANext4/Assets/Icons/asset-text-asset.png"),
                AssetClassID.Texture2D => GetBitmap("UABEANext4/Assets/Icons/asset-texture2d.png"),
                AssetClassID.Texture3D => GetBitmap("UABEANext4/Assets/Icons/asset-texture2d.png"),
                AssetClassID.Transform => GetBitmap("UABEANext4/Assets/Icons/asset-transform.png"),
                _ => GetBitmap("UABEANext4/Assets/Icons/asset-unknown.png"),
            };
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    Dictionary<string, Bitmap> cache = new();

    private Bitmap GetBitmap(string path)
    {
        Bitmap? bitmap;
        if (cache.TryGetValue(path, out bitmap))
        {
            return bitmap;
        }
        else
        {
            bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://{path}")));
            cache[path] = bitmap;
            return bitmap;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
