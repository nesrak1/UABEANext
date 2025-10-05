using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System.Runtime.InteropServices;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.Mesh;

namespace TexturePlugin.Helpers;
public class TextureLoader
{
    private readonly Dictionary<AssetInst, SKBitmap> _spriteBitmapCache = [];
    private readonly Queue<AssetInst> _spriteBitmapQueue = new();
    private readonly SpriteAtlasLookup _spriteAtlasLookup = new();
    private readonly Dictionary<AssetsFileInstance, Dictionary<string, AssetInst>> _nameToSpriteAtlasLookup = [];

    // todo: this should be configurable
    public const int DEFAULT_MAX_SPRITE_BITMAP_CACHE_SIZE = 10;

    public Bitmap? GetSpriteAvaloniaBitmap(Workspace workspace, AssetInst asset, out TextureFormat format)
    {
        SKBitmap? skBitmap = GetSpriteSkBitmap(workspace, asset, out format);
        if (skBitmap == null)
        {
            return null;
        }

        var croppedByteSize = skBitmap.Width * skBitmap.Height * 4;
        var bitmap = new WriteableBitmap(new PixelSize(skBitmap.Width, skBitmap.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var croppedPixels = skBitmap.PeekPixels();
        using var frameBuffer = bitmap.Lock();
        {
            var destByteSize = frameBuffer.RowBytes * frameBuffer.Size.Height;
            unsafe
            {
                // marshal.copy can't do native -> native so we have to do this unsafe copy
                Buffer.MemoryCopy(croppedPixels.GetPixels().ToPointer(), frameBuffer.Address.ToPointer(), destByteSize, croppedByteSize);
            }
        }

        skBitmap.Dispose();
        return bitmap;
    }

    public byte[]? GetSpriteRawBytes(
        Workspace workspace, AssetInst asset,
        out TextureFormat format, out int width, out int height)
    {
        SKBitmap? skBitmap = GetSpriteSkBitmap(workspace, asset, out format);
        if (skBitmap == null)
        {
            width = 0;
            height = 0;
            return null;
        }

        width = skBitmap.Width;
        height = skBitmap.Height;

        byte[] outData = new byte[width * height * 4];
        using (var croppedPixels = skBitmap.PeekPixels())
        {
            Marshal.Copy(croppedPixels.GetPixels(), outData, 0, outData.Length);
        }

        skBitmap.Dispose();
        return outData;
    }

    // format output is only for error messages. the byte output is rgba32.
    public SKBitmap? GetSpriteSkBitmap(Workspace workspace, AssetInst asset, out TextureFormat format)
    {
        format = 0;

        var spriteBf = workspace.GetBaseField(asset);
        if (spriteBf == null)
        {
            return null;
        }

        var renderData = spriteBf["m_RD"];
        var spriteAtlas = GetSpriteAtlas(workspace, asset, spriteBf);

        AssetPPtr texturePtr;
        if (spriteAtlas != null)
        {
            texturePtr = spriteAtlas.texture;
        }
        else
        {
            texturePtr = AssetPPtr.FromField(renderData["texture"]);
            if (texturePtr.IsNull())
            {
                return null;
            }
        }

        var textureAsset = workspace.GetAssetInst(asset.FileInstance, texturePtr.FileId, texturePtr.PathId);
        if (textureAsset == null)
        {
            return null;
        }

        // we use skia so we can crop, then convert to avalonia bitmap at the end
        SKBitmap baseBitmap;
        if (_spriteBitmapCache.TryGetValue(textureAsset, out var cachedBitmap))
        {
            baseBitmap = cachedBitmap;
        }
        else
        {
            var textureEditBf = TextureHelper.GetByteArrayTexture(workspace, textureAsset);
            var texture = TextureFile.ReadTextureFile(textureEditBf);
            format = (TextureFormat)texture.m_TextureFormat;

            TextureHelper.SwizzleOptIn(texture, textureAsset.FileInstance.file);

            var encTextureData = texture.FillPictureData(textureAsset.FileInstance);
            var textureData = texture.DecodeTextureRaw(encTextureData);
            if (textureData == null)
            {
                return null;
            }

            baseBitmap = new SKBitmap(texture.m_Width, texture.m_Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var basePixels = baseBitmap.PeekPixels();
            var basePixelsSpan = basePixels.GetPixelSpan<byte>();
            MemoryExtensions.CopyTo(textureData, basePixelsSpan);

            // just like the lz4 block decoder, this only pulls whichever item
            // was added earliest since we can't reset the position of elements
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

        var pivot = spriteBf["m_Pivot"];
        var pivotX = pivot["x"].AsFloat;
        var pivotY = pivot["y"].AsFloat;

        var rect = spriteBf["m_Rect"];
        var rectWidth = rect["width"].AsFloat;
        var rectHeight = rect["height"].AsFloat;

        float textureRectOffsetX, textureRectOffsetY;
        float textureRectX, textureRectY, textureRectWidth, textureRectHeight;
        uint settingsRaw;

        if (spriteAtlas != null)
        {
            textureRectX = spriteAtlas.textureRectX;
            textureRectY = spriteAtlas.textureRectY;
            textureRectWidth = spriteAtlas.textureRectWidth;
            textureRectHeight = spriteAtlas.textureRectHeight;

            textureRectOffsetX = spriteAtlas.textureRectOffsetX;
            textureRectOffsetY = spriteAtlas.textureRectOffsetY;

            settingsRaw = spriteAtlas.settingsRaw;
        }
        else
        {
            var textureRect = renderData["textureRect"];
            textureRectX = (float)Math.Floor(textureRect["x"].AsFloat);
            textureRectY = (float)Math.Floor(textureRect["y"].AsFloat);
            textureRectWidth = (float)Math.Ceiling(textureRect["width"].AsFloat);
            textureRectHeight = (float)Math.Ceiling(textureRect["height"].AsFloat);

            var textureRectOffset = renderData["textureRectOffset"];
            textureRectOffsetX = textureRectOffset["x"].AsFloat;
            textureRectOffsetY = textureRectOffset["y"].AsFloat;

            settingsRaw = renderData["settingsRaw"].AsUInt;
        }

        // todo
        var flipX = (settingsRaw & 4) != 0;
        var flipY = (settingsRaw & 8) != 0;
        var rot90 = (settingsRaw & 16) != 0;

        var croppedBitmap = new SKBitmap((int)Math.Round(textureRectWidth), (int)Math.Round(textureRectHeight));

        var version = asset.FileInstance.file.Metadata.UnityVersion;
        var mesh = new MeshObj(asset.FileInstance, renderData, new UnityVersion(version));
        if (mesh.Vertices.Length % 3 != 0)
        {
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

                if (flipX)
                {
                    canvas.Translate(croppedBitmap.Width, 0);
                    canvas.Scale(-1, 1);
                }
                if (flipY)
                {
                    canvas.Translate(0, croppedBitmap.Height);
                    canvas.Scale(1, -1);
                }
                // todo: rot90

                canvas.DrawBitmap(baseBitmap, -textureRectX, -textureRectY);
            }
        }

        return croppedBitmap;
    }

    private SpriteAtlasData? GetSpriteAtlas(Workspace workspace, AssetInst asset, AssetTypeValueField spriteBf)
    {
        var spriteAtlas = spriteBf["m_SpriteAtlas"];
        var spriteAtlasPtr = AssetPPtr.FromField(spriteAtlas);
        if (spriteAtlasPtr.IsNull())
        {
            var atlasTags = spriteBf["m_AtlasTags.Array"];
            if (atlasTags.Children.Count == 0)
            {
                // nothing we can do. there's no reference to an atlas/texture anywhere.
                return null;
            }

            // in some games, m_SpriteAtlas is not set, but a SpriteAtlas in the same
            // file references this sprite. m_AtlasTags has a list of atlas names.
            // I am not sure why this list would have multiple entries. this field may
            // have more than one only in an editor project.
            var atlasTag = atlasTags[0].AsString;

            // we're going to assume the sprite atlas is always in the same file.
            // it would probably be good to do a last resort option, but tbd on that.
            var atlasNameLookup = GetSpriteAtlasNameLookup(workspace, asset.FileInstance);

            var atlasAsset = atlasNameLookup[atlasTag];
            if (atlasAsset is null)
            {
                // nothing we can do. give up.
                return null;
            }

            spriteAtlasPtr = new AssetPPtr(0, atlasAsset.PathId);
        }

        spriteAtlasPtr.SetFilePathFromFile(workspace.Manager, asset.FileInstance);
        var key = SpriteAtlasLookup.MakeRenderKeyGuid(spriteBf["m_RenderDataKey"]["first"]);
        var atlasData = _spriteAtlasLookup.GetAtlasData(spriteAtlasPtr, key);
        if (atlasData != null)
        {
            return atlasData;
        }

        var spriteAtlasBf = workspace.GetBaseField(asset.FileInstance, spriteAtlasPtr.FileId, spriteAtlasPtr.PathId);
        if (spriteAtlasBf == null)
        {
            return null;
        }

        _spriteAtlasLookup.AddSpriteAtlas(spriteAtlasPtr, spriteAtlasBf);
        return _spriteAtlasLookup.GetAtlasData(spriteAtlasPtr, key);
    }

    private Dictionary<string, AssetInst> GetSpriteAtlasNameLookup(Workspace workspace, AssetsFileInstance fileInstance)
    {
        if (_nameToSpriteAtlasLookup.TryGetValue(fileInstance, out var nameLookup))
        {
            return nameLookup;
        }

        var atlasNameLookup = new Dictionary<string, AssetInst>();
        foreach (var atlasInf in fileInstance.file.GetAssetsOfType(AssetClassID.SpriteAtlas))
        {
            // cast is ok, file.Metadata.AssetInfos is always a AssetInst list when loaded by workspace
            var atlasAsset = (AssetInst)atlasInf;

            var atlasBf = workspace.GetBaseField(atlasAsset);
            if (atlasBf is null)
                continue;

            var atlasTag = atlasBf["m_Tag"].AsString;
            atlasNameLookup[atlasTag] = atlasAsset;
        }

        _nameToSpriteAtlasLookup[fileInstance] = atlasNameLookup;
        return atlasNameLookup;
    }

    public static Bitmap? GetTexture2DBitmap(Workspace workspace, AssetInst asset, out TextureFormat format)
    {
        var textureEditBf = TextureHelper.GetByteArrayTexture(workspace, asset);
        var texture = TextureFile.ReadTextureFile(textureEditBf);
        format = (TextureFormat)texture.m_TextureFormat;

        TextureHelper.SwizzleOptIn(texture, asset.FileInstance.file);

        var encTextureData = texture.FillPictureData(asset.FileInstance);
        // rare, but sometimes we see large textures with 0 texture data size
        if (encTextureData.Length == 0 || (texture.m_Width == 0 && texture.m_Height == 0))
        {
            return null;
        }

        var textureData = texture.DecodeTextureRaw(encTextureData);
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

    public void Cleanup()
    {
        foreach (var bitmap in _spriteBitmapCache.Values)
        {
            bitmap.Dispose();
        }
        _spriteBitmapCache.Clear();
        _spriteBitmapQueue.Clear();
        _spriteAtlasLookup.Clear();
        _nameToSpriteAtlasLookup.Clear();
    }
}
