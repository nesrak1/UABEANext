using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Sprite;

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

    private TexturePreview _textureLoader;

    const int TEXT_ASSET_MAX_LENGTH = 100000;

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public PreviewerToolViewModel()
    {
        Workspace = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeImage = null;
        _activeDocument = new TextDocument();
        _textureLoader = new TexturePreview();
    }

    public PreviewerToolViewModel(Workspace workspace)
    {
        Workspace = workspace;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeImage = null;
        _activeDocument = new TextDocument("No preview available.");
        _textureLoader = new TexturePreview();

        WeakReferenceMessenger.Default.Register<AssetsSelectedMessage>(this, OnAssetsSelected);
        WeakReferenceMessenger.Default.Register<WorkspaceClosingMessage>(this, OnWorkspaceClosing);
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

    private void OnWorkspaceClosing(object recipient, WorkspaceClosingMessage message)
    {
        HandleAssetPreview(null);
    }

    private void HandleAssetPreview(AssetInst? asset)
    {
        if (asset != null)
        {
            if (asset.Type == AssetClassID.Texture2D)
            {
                ActivePreviewType = PreviewerToolPreviewType.Image;
                DisposeCurrentImage();

                var image = _textureLoader.GetTexture2DBitmap(Workspace, asset, out TextureFormat format);
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
                return;
            }
            else if (asset.Type == AssetClassID.Sprite)
            {
                ActivePreviewType = PreviewerToolPreviewType.Image;
                DisposeCurrentImage();

                var image = _textureLoader.GetSpriteBitmap(Workspace, asset, out TextureFormat format);
                if (image != null)
                {
                    ActiveImage = image;
                }
                else
                {
                    ActivePreviewType = PreviewerToolPreviewType.Text;
                    ActiveDocument = new TextDocument(
                        $"Sprite failed to decode. Image format may not be supported. ({format})");
                }
                return;
            }
            else if (asset.Type == AssetClassID.TextAsset)
            {
                ActivePreviewType = PreviewerToolPreviewType.Text;
                ActiveDocument = new TextDocument(GetTextAssetText(asset));
                return;
            }
        }
        ActivePreviewType = PreviewerToolPreviewType.Text;
        ActiveDocument = new TextDocument("No preview available.");
    }

    private void DisposeCurrentImage()
    {
        if (ActiveImage != null)
        {
            ActiveImage.Dispose();
            ActiveImage = null;
        }
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
