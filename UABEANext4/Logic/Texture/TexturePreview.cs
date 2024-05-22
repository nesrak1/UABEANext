using AssetsTools.NET;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Logic.Sprite;
public class TexturePreview
{
    private readonly Dictionary<AssetInst, SKBitmap> _spriteBitmapCache = new();
    private readonly Queue<AssetInst> _spriteBitmapQueue = new();

    public const int DEFAULT_MAX_SPRITE_BITMAP_CACHE_SIZE = 10;

    public Bitmap? GetSpriteBitmap(Workspace workspace, AssetInst asset, out TextureFormat format)
    {
        var spriteBf = workspace.GetBaseField(asset);
        if (spriteBf == null)
        {
            format = 0;
            return null;
        }

        var renderData = spriteBf["m_RD"];
        var texturePtr = renderData["texture"];
        if (AssetPPtr.FromField(texturePtr).IsNull())
        {
            format = 0;
            return null;
        }

        var textureAsset = workspace.GetAssetInst(asset.FileInstance, texturePtr);
        if (textureAsset == null)
        {
            format = 0;
            return null;
        }

        // we use skia so we can crop, then convert to avalonia bitmap at the end
        SKBitmap baseBitmap;
        if (_spriteBitmapCache.TryGetValue(textureAsset, out var cachedBitmap))
        {
            baseBitmap = cachedBitmap;
            format = 0;
        }
        else
        {
            var textureEditBf = GetByteArrayTexture(workspace, textureAsset);
            var texture = TextureFile.ReadTextureFile(textureEditBf);
            format = (TextureFormat)texture.m_TextureFormat;

            var textureData = texture.GetTextureData(textureAsset.FileInstance);
            if (textureData == null)
            {
                format = 0;
                return null;
            }

            baseBitmap = new SKBitmap(texture.m_Width, texture.m_Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var basePixels = baseBitmap.PeekPixels();
            var basePixelsSpan = basePixels.GetPixelSpan<byte>();
            MemoryExtensions.CopyTo(textureData, basePixelsSpan);

            // just like the lz4 block decoder, this only pulls whichever item
            // was added last since we can't reset the position of elements
            // with a stock .net queue
            if (_spriteBitmapQueue.Count >= DEFAULT_MAX_SPRITE_BITMAP_CACHE_SIZE)
            {
                var lastKey = _spriteBitmapQueue.Dequeue();
                var lastValue = _spriteBitmapCache[lastKey];
                lastValue.Dispose();
                _spriteBitmapCache.Remove(lastKey);
            }

            _spriteBitmapCache[textureAsset] = baseBitmap;
            _spriteBitmapQueue.Enqueue(textureAsset);
        }

        var pixelsToUnits = spriteBf["m_PixelsToUnits"].AsFloat;

        var rect = spriteBf["m_Rect"];
        var rectWidth = rect["width"].AsFloat;
        var rectHeight = rect["height"].AsFloat;

        var pivot = spriteBf["m_Pivot"];
        var pivotX = pivot["x"].AsFloat;
        var pivotY = pivot["y"].AsFloat;

        var textureRectOffset = renderData["textureRectOffset"];
        var textureRectOffsetX = textureRectOffset["x"].AsFloat;
        var textureRectOffsetY = textureRectOffset["y"].AsFloat;

        var textureRect = renderData["textureRect"];
        var textureRectX = (float)Math.Floor(textureRect["x"].AsFloat);
        var textureRectY = (float)Math.Floor(textureRect["y"].AsFloat);
        var textureRectWidth = (float)Math.Ceiling(textureRect["width"].AsFloat);
        var textureRectHeight = (float)Math.Ceiling(textureRect["height"].AsFloat);

        // todo
        var settingsRaw = renderData["settingsRaw"].AsInt;
        var flipX = (settingsRaw & 4) != 0;
        var flipY = (settingsRaw & 8) != 0;
        var rot90 = (settingsRaw & 16) != 0;

        using var croppedBitmap = new SKBitmap((int)Math.Round(textureRectWidth), (int)Math.Round(textureRectHeight));

        var mesh = new MeshReader(asset.FileInstance, renderData);
        if (mesh.Vertices.Length % 3 != 0)
        {
            format = 0;
            return null;
        }

        using (var canvas = new SKCanvas(croppedBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using (var path = new SKPath())
            {
                var offX = (rectWidth * pivotX) - textureRectOffsetX;
                var offY = (rectHeight * pivotY) - textureRectOffsetY;
                for (var i = 0; i < mesh.Indices.Length; i += 3)
                {
                    var pointAIdx = mesh.Indices[i] * 3;
                    var pointBIdx = mesh.Indices[i + 1] * 3;
                    var pointCIdx = mesh.Indices[i + 2] * 3;
                    var pointA = new SKPoint(
                        mesh.Vertices[pointAIdx] * pixelsToUnits + offX,
                        mesh.Vertices[pointAIdx + 1] * pixelsToUnits + offY
                    );
                    var pointB = new SKPoint(
                        mesh.Vertices[pointBIdx] * pixelsToUnits + offX,
                        mesh.Vertices[pointBIdx + 1] * pixelsToUnits + offY
                    );
                    var pointC = new SKPoint(
                        mesh.Vertices[pointCIdx] * pixelsToUnits + offX,
                        mesh.Vertices[pointCIdx + 1] * pixelsToUnits + offY
                    );
                    var points = new SKPoint[] { pointA, pointB, pointC };
                    path.AddPoly(points);
                }
                canvas.ClipPath(path);
                canvas.DrawBitmap(baseBitmap, -textureRectX, -textureRectY);
            }
        }

        var croppedByteSize = croppedBitmap.Width * croppedBitmap.Height * 4;
        var bitmap = new WriteableBitmap(new PixelSize(croppedBitmap.Width, croppedBitmap.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var croppedPixels = croppedBitmap.PeekPixels();
        using (var frameBuffer = bitmap.Lock())
        {
            var destByteSize = frameBuffer.RowBytes * frameBuffer.Size.Height;
            unsafe
            {
                // marshal.copy can't do native -> native so we have to do this unsafe copy
                Buffer.MemoryCopy(croppedPixels.GetPixels().ToPointer(), frameBuffer.Address.ToPointer(), destByteSize, croppedByteSize);
            }
        }

        return bitmap;
    }

    public Bitmap? GetTexture2DBitmap(Workspace workspace, AssetInst asset, out TextureFormat format)
    {
        var textureEditBf = GetByteArrayTexture(workspace, asset);
        var texture = TextureFile.ReadTextureFile(textureEditBf);
        format = (TextureFormat)texture.m_TextureFormat;

        var textureData = texture.GetTextureData(asset.FileInstance);
        if (textureData == null)
        {
            return null;
        }

        var bitmap = new WriteableBitmap(new PixelSize(texture.m_Width, texture.m_Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var frameBuffer = bitmap.Lock())
        {
            Marshal.Copy(textureData, 0, frameBuffer.Address, textureData.Length);
        }

        return bitmap;
    }

    private static AssetTypeValueField? GetByteArrayTexture(Workspace workspace, AssetInst tex)
    {
        var textureTemp = workspace.GetTemplateField(tex.FileInstance, tex);
        var imageData = textureTemp.Children.FirstOrDefault(f => f.Name == "image data");
        if (imageData == null)
            return null;

        imageData.ValueType = AssetValueType.ByteArray;

        var platformBlob = textureTemp.Children.FirstOrDefault(f => f.Name == "m_PlatformBlob");
        if (platformBlob != null)
        {
            var m_PlatformBlob_Array = platformBlob.Children[0];
            m_PlatformBlob_Array.ValueType = AssetValueType.ByteArray;
        }

        var baseField = textureTemp.MakeValue(tex.FileReader, tex.AbsoluteByteStart);
        return baseField;
    }

    public void Cleanup()
    {
        foreach (var bitmap in _spriteBitmapCache.Values)
        {
            bitmap.Dispose();
        }
        _spriteBitmapCache.Clear();
    }
}
