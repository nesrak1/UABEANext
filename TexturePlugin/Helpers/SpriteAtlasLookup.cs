using AssetsTools.NET;

namespace TexturePlugin.Helpers;
public class SpriteAtlasLookup
{
    public readonly Dictionary<AssetPPtr, Dictionary<GUID128, SpriteAtlasData>> _lookup = [];

    public SpriteAtlasData? GetAtlasData(AssetPPtr atlasPtr, GUID128 key)
    {
        if (_lookup.TryGetValue(atlasPtr, out var atlasLookup))
        {
            if (atlasLookup.TryGetValue(key, out var atlasData))
            {
                return atlasData;
            }
        }
        return null;
    }

    public void AddSpriteAtlas(AssetPPtr atlasPtr, AssetTypeValueField atlasBf)
    {
        if (_lookup.ContainsKey(atlasPtr))
        {
            return;
        }

        var pairs = atlasBf["m_RenderDataMap.Array"];
        _lookup[atlasPtr] = [];
        foreach (var pair in pairs)
        {
            var guidField = pair["first.first"];
            var key = MakeRenderKeyGuid(guidField);
            var value = new SpriteAtlasData(pair["second"]);
            _lookup[atlasPtr][key] = value;
        }
    }

    public void Clear()
    {
        _lookup.Clear();
    }

    public static GUID128 MakeRenderKeyGuid(AssetTypeValueField field)
    {
        return new GUID128()
        {
            data0 = field[0].AsUInt,
            data1 = field[1].AsUInt,
            data2 = field[2].AsUInt,
            data3 = field[3].AsUInt,
        };
    }
}

public class SpriteAtlasData
{
    public AssetPPtr texture;
    public AssetPPtr alphaTexture;
    // todo: make these vectors
    public float textureRectX;
    public float textureRectY;
    public float textureRectWidth;
    public float textureRectHeight;
    public float textureRectOffsetX;
    public float textureRectOffsetY;
    public float atlasRectOffsetX;
    public float atlasRectOffsetY;
    public float uvTransformX;
    public float uvTransformY;
    public float uvTransformZ;
    public float uvTransformW;
    public float downscaleMultiplier;
    public uint settingsRaw;
    public SpriteAtlasData(AssetTypeValueField field)
    {
        texture = AssetPPtr.FromField(field["texture"]);
        alphaTexture = AssetPPtr.FromField(field["alphaTexture"]);
        var textureRect = field["textureRect"];
        textureRectX = textureRect["x"].AsFloat;
        textureRectY = textureRect["y"].AsFloat;
        textureRectWidth = textureRect["width"].AsFloat;
        textureRectHeight = textureRect["height"].AsFloat;
        var textureRectOffset = field["textureRectOffset"];
        textureRectOffsetX = textureRectOffset["x"].AsFloat;
        textureRectOffsetY = textureRectOffset["y"].AsFloat;
        var atlasRectOffset = field["atlasRectOffset"];
        atlasRectOffsetX = atlasRectOffset["x"].AsFloat;
        atlasRectOffsetY = atlasRectOffset["y"].AsFloat;
        var uvTransform = field["uvTransform"];
        uvTransformX = uvTransform["x"].AsFloat;
        uvTransformY = uvTransform["y"].AsFloat;
        uvTransformZ = uvTransform["z"].AsFloat;
        uvTransformW = uvTransform["w"].AsFloat;
        downscaleMultiplier = field["downscaleMultiplier"].AsFloat;
        settingsRaw = field["settingsRaw"].AsUInt;
    }
}