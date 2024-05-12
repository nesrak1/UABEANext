using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;

namespace UABEANext4.ViewModels.Tools;
public partial class PreviewerToolViewModel : Tool
{
    const string TOOL_TITLE = "Previewer";

    public Workspace Workspace { get; }

    [ObservableProperty]
    public Bitmap? _activeImage;
    [ObservableProperty]
    public TextDocument? _activeDocument;

    [ObservableProperty]
    public PreviewerToolPreviewType _activePreviewType = PreviewerToolPreviewType.Text;

    const int TEXT_ASSET_MAX_LENGTH = 100000;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public PreviewerToolViewModel()
    {
        Workspace = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeImage = null;
        _activeDocument = new TextDocument();
    }

    public PreviewerToolViewModel(Workspace workspace)
    {
        Workspace = workspace;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeImage = null;
        _activeDocument = new TextDocument("No preview available.");

        WeakReferenceMessenger.Default.Register<AssetsSelectedMessage>(this, OnAssetsSelected);
    }

    private void OnAssetsSelected(object recipient, AssetsSelectedMessage message)
    {
        var assets = message.Value;
        if (assets.Count == 0)
        {
            return;
        }

        var asset = assets[0];
        HandleAssetPreview(asset);
    }

    private void HandleAssetPreview(AssetInst asset)
    {
        if (asset.Type == AssetClassID.Texture2D)
        {
            ActivePreviewType = PreviewerToolPreviewType.Image;
            if (ActiveImage != null)
            {
                ActiveImage.Dispose();
                ActiveImage = null;
            }

            var image = GetTexture2DBitmap(asset, out TextureFormat format);
            if (image != null)
            {
                ActiveImage = image;
            }
            else
            {
                ActivePreviewType = PreviewerToolPreviewType.Text;
                ActiveDocument = new TextDocument(
                    $"Texture failed to decode. Image format may not be supported. ({format})");
            }
        }
        else if (asset.Type == AssetClassID.TextAsset)
        {
            ActivePreviewType = PreviewerToolPreviewType.Text;
            ActiveDocument = new TextDocument(GetTextAssetText(asset));
        }
        else
        {
            ActivePreviewType = PreviewerToolPreviewType.Text;
            ActiveDocument = new TextDocument("No preview available.");
        }
    }

    private Bitmap? GetTexture2DBitmap(AssetInst asset, out TextureFormat format)
    {
        var textureEditBf = GetByteArrayTexture(Workspace, asset);
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

    public static AssetTypeValueField? GetByteArrayTexture(Workspace workspace, AssetInst tex)
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

    private string GetTextAssetText(AssetInst asset)
    {
        var baseField = Workspace.GetBaseField(asset);
        if (baseField == null)
        {
            return "No preview available.";
        }

        var text = baseField["m_Script"].AsByteArray;
        string trimmedText;
        if (text.Length <= TEXT_ASSET_MAX_LENGTH)
        {
            trimmedText = Encoding.UTF8.GetString(text);
        }
        else
        {
            trimmedText = Encoding.UTF8.GetString(text[..TEXT_ASSET_MAX_LENGTH]) + $"... (and {text.Length - TEXT_ASSET_MAX_LENGTH} bytes more)";
        }

        return trimmedText;
    }
}

public enum PreviewerToolPreviewType
{
    Image,
    Text,
    Mesh,
}
