using AssetsTools.NET;
using AssetsTools.NET.Texture;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using UABEANext4.AssetWorkspace;
using UABEANext4.Interfaces;
using UABEANext4.Util;
using UABEANext4.ViewModels;
using ColorSpaceEnm = TexturePlugin.Logic.EditTexture.ColorSpace;
using FilterModeEnm = TexturePlugin.Logic.EditTexture.FilterMode;
using TextureFormatEnm = AssetsTools.NET.Texture.TextureFormat;
using WrapModeEnm = TexturePlugin.Logic.EditTexture.WrapMode;

// this could be uh... improved... but it'll work for now :D
namespace TexturePlugin.ViewModels;
public partial class EditTextureViewModel : ViewModelBaseValidator, IDialogAware<EditTextureResult?>
{
    [ObservableProperty]
    public bool _isSingleTexture;

    // field properties
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NameWatermark))]
    public string? _name = "";

    [ObservableProperty]
    public TextureFormatEnm? _textureFormat = TextureFormatEnm.RGBA32;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsingMipsThreeOn))]
    public bool? _usingMips = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadableThreeOn))]
    public bool? _isReadable = false;

    [ObservableProperty]
    public FilterModeEnm? _filterMode = FilterModeEnm.Point;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteringWatermark))]
    [CustomValidation(typeof(EditTextureViewModel), nameof(ValidateInt))]
    public string? _filteringString = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MipBiasWatermark))]
    [CustomValidation(typeof(EditTextureViewModel), nameof(ValidateInt))]
    public string? _mipBiasString = "0";

    [ObservableProperty]
    public WrapModeEnm? _wrapModeU = WrapModeEnm.Repeat;

    [ObservableProperty]
    public WrapModeEnm? _wrapModeV = WrapModeEnm.Repeat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LightMapFormatWatermark))]
    [CustomValidation(typeof(EditTextureViewModel), nameof(ValidateInt))]
    public string? _lightMapFormatString = "0";

    [ObservableProperty]
    public ColorSpaceEnm? _colorSpace = ColorSpaceEnm.Gamma;

    // default fields
    private string? _defaultName;
    private TextureFormatEnm? _defaultTextureFormat;
    private bool? _defaultUsingMips;
    private bool? _defaultIsReadable;
    private FilterModeEnm? _defaultFilterMode;
    private string? _defaultFilteringString;
    private string? _defaultMipBiasString;
    private WrapModeEnm? _defaultWrapModeU;
    private WrapModeEnm? _defaultWrapModeV;
    private string? _defaultLightMapFormatString;
    private ColorSpaceEnm? _defaultColorSpace;

    // textbox watermarks
    public string NameWatermark => Name is null ? "(Multiple values)" : "";
    public string FilteringWatermark => FilteringString is null ? "(Multiple values)" : "";
    public string MipBiasWatermark => MipBiasString is null ? "(Multiple values)" : "";
    public string LightMapFormatWatermark => LightMapFormatString is null ? "(Multiple values)" : "";

    // checkbox three-states
    public bool UsingMipsThreeOn => UsingMips is null;
    public bool IsReadableThreeOn => IsReadable is null;

    // combobox options
    public static TextureFormatEnm[] TextureFormats => Enum.GetValues<TextureFormatEnm>();
    public static FilterModeEnm[] FilterModes => Enum.GetValues<FilterModeEnm>();
    public static WrapModeEnm[] WrapModes => Enum.GetValues<WrapModeEnm>();
    public static ColorSpaceEnm[] ColorSpaces => Enum.GetValues<ColorSpaceEnm>();

    public string Title => "Texture Edit";
    public int Width => 450;
    public int Height => 500;

    public bool IsModal => true;

    public event Action<EditTextureResult?>? RequestClose;

    private readonly List<(AssetInst, AssetTypeValueField, TextureFile)> _textures = [];

    public EditTextureViewModel(Workspace workspace, IList<AssetInst> assets)
    {
        IsSingleTexture = assets.Count == 1;

        foreach (var asset in assets)
        {
            var baseField = workspace.GetBaseField(asset);
            if (baseField is null)
                continue;

            var textureFile = TextureFile.ReadTextureFile(baseField);
            _textures.Add((asset, baseField, textureFile));
        }

        SetDefaultValues();
    }

    public void SetDefaultValues()
    {
        string? name;
        TextureFormatEnm? textureFormat;
        bool? usingMips;
        bool? isReadable;
        FilterModeEnm? filterMode;
        int? filtering;
        float? mipBias;
        WrapModeEnm? wrapModeU;
        WrapModeEnm? wrapModeV;
        int? lightMapFormat;
        ColorSpaceEnm? colorSpace;

        // can't do anything
        if (_textures.Count == 0)
            return;

        var firstTexture = _textures[0];
        var firstTextureFile = firstTexture.Item3;
        var firstTextureSettings = firstTextureFile.m_TextureSettings;
        name = firstTextureFile.m_Name;
        textureFormat = (TextureFormatEnm)firstTextureFile.m_TextureFormat;
        usingMips = firstTextureFile.m_MipMap;
        isReadable = firstTextureFile.m_IsReadable;
        filterMode = (FilterModeEnm)firstTextureSettings.m_FilterMode;
        filtering = firstTextureSettings.m_Aniso;
        mipBias = firstTextureSettings.m_MipBias;
        wrapModeU = (WrapModeEnm)firstTextureSettings.m_WrapU;
        wrapModeV = (WrapModeEnm)firstTextureSettings.m_WrapV;
        lightMapFormat = firstTextureFile.m_LightmapFormat;
        colorSpace = (ColorSpaceEnm)firstTextureFile.m_ColorSpace;

        if (_textures.Count > 1)
        {
            for (var i = 1; i < _textures.Count; i++)
            {
                var texture = _textures[i];
                var textureFile = texture.Item3;
                var textureSettings = textureFile.m_TextureSettings;

                if (name != textureFile.m_Name)
                    name = null;
                if (textureFormat != (TextureFormatEnm)textureFile.m_TextureFormat)
                    textureFormat = null;
                if (usingMips != textureFile.m_MipMap)
                    usingMips = null;
                if (isReadable != textureFile.m_IsReadable)
                    isReadable = null;
                if (filterMode != (FilterModeEnm)textureSettings.m_FilterMode)
                    filterMode = null;
                if (filtering != textureSettings.m_Aniso)
                    filtering = null;
                if (mipBias != textureSettings.m_MipBias)
                    mipBias = null;
                if (wrapModeU != (WrapModeEnm)textureSettings.m_WrapU)
                    wrapModeU = null;
                if (wrapModeV != (WrapModeEnm)textureSettings.m_WrapV)
                    wrapModeV = null;
                if (lightMapFormat != textureFile.m_LightmapFormat)
                    lightMapFormat = null;
                if (colorSpace != (ColorSpaceEnm)textureFile.m_ColorSpace)
                    colorSpace = null;
            }
        }

        _defaultName = Name = name;
        _defaultTextureFormat = TextureFormat = textureFormat;
        _defaultUsingMips = UsingMips = usingMips;
        _defaultIsReadable = IsReadable = isReadable;
        _defaultFilterMode = FilterMode = filterMode;
        _defaultFilteringString = FilteringString = filtering?.ToString();
        _defaultMipBiasString = MipBiasString = mipBias?.ToString();
        _defaultWrapModeU = WrapModeU = wrapModeU;
        _defaultWrapModeV = WrapModeV = wrapModeV;
        _defaultLightMapFormatString = LightMapFormatString = lightMapFormat?.ToString();
        _defaultColorSpace = ColorSpace = colorSpace;
    }

    public void ResetToDefault(object param)
    {
        if (param is not string paramStr || !int.TryParse(paramStr, out int index))
            return;

        switch (index)
        {
            case 0: Name = _defaultName; break;
            case 1: TextureFormat = _defaultTextureFormat; break;
            case 2: UsingMips = _defaultUsingMips; break;
            case 3: IsReadable = _defaultIsReadable; break;
            case 4: FilterMode = _defaultFilterMode; break;
            case 5: FilteringString = _defaultFilteringString; break;
            case 6: MipBiasString = _defaultMipBiasString; break;
            case 7: WrapModeU = _defaultWrapModeU; break;
            case 8: WrapModeV = _defaultWrapModeV; break;
            case 9: LightMapFormatString = _defaultLightMapFormatString; break;
            case 10: ColorSpace = _defaultColorSpace; break;
        }
    }

    public async void SaveChanges()
    {
        int? filtering;
        if (FilteringString is not null)
        {
            if (!int.TryParse(FilteringString, out var filteringTmp))
            {
                await ShowInvalidOptionsBox();
                return;
            }
            filtering = filteringTmp;
        }
        else
        {
            filtering = null;
        }

        int? mipBias;
        if (MipBiasString is not null)
        {
            if (!int.TryParse(MipBiasString, out var mipBiasTmp))
            {
                await ShowInvalidOptionsBox();
                return;
            }
            mipBias = mipBiasTmp;
        }
        else
        {
            mipBias = null;
        }

        int? lightMapFormat;
        if (LightMapFormatString is not null)
        {
            if (!int.TryParse(LightMapFormatString, out var lightMapFormatTmp))
            {
                await ShowInvalidOptionsBox();
                return;
            }
            lightMapFormat = lightMapFormatTmp;
        }
        else
        {
            lightMapFormat = null;
        }

        RequestClose?.Invoke(
            new EditTextureResult(
                Name,
                TextureFormat,
                UsingMips,
                IsReadable,
                FilterMode,
                filtering,
                mipBias,
                WrapModeU,
                WrapModeV,
                lightMapFormat,
                ColorSpace
            )
        );
    }

    public void Cancel()
    {
        RequestClose?.Invoke(null);
    }

    public static ValidationResult? ValidateInt(string intStr, ValidationContext context)
    {
        if (!string.IsNullOrEmpty(intStr) && !uint.TryParse(intStr, out var _))
        {
            return new("Value must be an int");
        }

        return ValidationResult.Success;
    }

    private async Task ShowInvalidOptionsBox()
    {
        await MessageBoxUtil.ShowDialog("Error", "Invalid options provided.");
    }
}

public readonly struct EditTextureResult(
    string? name,
    TextureFormatEnm? textureFormat,
    bool? usingMips,
    bool? isReadable,
    FilterModeEnm? filterMode,
    int? filtering,
    int? mipBias,
    WrapModeEnm? wrapModeU,
    WrapModeEnm? wrapModeV,
    int? lightMapFormat,
    ColorSpaceEnm? colorSpace)
{
    public readonly string? Name = name;
    public readonly TextureFormatEnm? TextureFormat = textureFormat;
    public readonly bool? UsingMips = usingMips;
    public readonly bool? IsReadable = isReadable;
    public readonly FilterModeEnm? FilterMode = filterMode;
    public readonly int? Filtering = filtering;
    public readonly int? MipBias = mipBias;
    public readonly WrapModeEnm? WrapModeU = wrapModeU;
    public readonly WrapModeEnm? WrapModeV = wrapModeV;
    public readonly int? LightMapFormat = lightMapFormat;
    public readonly ColorSpaceEnm? ColorSpace = colorSpace;
}
