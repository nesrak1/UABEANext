using AssetsTools.NET.Texture;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Mvvm.Controls;
using System;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Logic.Mesh;
using UABEANext4.Plugins;

namespace UABEANext4.ViewModels.Tools;

public partial class PreviewerToolViewModel : Tool
{
    const string TOOL_TITLE = "Previewer";

    public Workspace Workspace { get; }
    [ObservableProperty]
    public TextDocument? _activeDocument;
    [ObservableProperty]
    public MeshObj? _activeMesh;

    [ObservableProperty]
    private PreviewerToolPreviewType _activePreviewType = PreviewerToolPreviewType.None;

    [ObservableProperty]
    public ImagePreviewViewModel _imagePreview = new();

    [ObservableProperty]
    public FontPreviewViewModel _fontPreview = new();

    // defer this to first preview since dialogs won't exist until after initial load
    private readonly Lazy<UavPluginFunctions> _uavPluginFuncs = new(() => new UavPluginFunctions());

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public PreviewerToolViewModel()
    {
        Workspace = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeDocument = new TextDocument();
        _activeMesh = new MeshObj();
        ActivePreviewType = PreviewerToolPreviewType.None;
    }

    public PreviewerToolViewModel(Workspace workspace)
    {
        Workspace = workspace;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeDocument = new TextDocument("No preview available.");
        
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
        if (asset is null)
        {
            SetDisplayNone();
            return;
        }

        var pluginsList = Workspace.Plugins.GetPreviewersThatSupport(Workspace, asset);
        if (pluginsList == null || pluginsList.Count == 0)
        {
            SetDisplayNone();
            return;
        }

        var firstPrevPair = pluginsList[0];
        var prevType = firstPrevPair.PreviewType;
        var prev = firstPrevPair.Previewer;

        try
        {
            var result = prev.Execute(Workspace, _uavPluginFuncs.Value, asset);

            switch (result)
            {
                case PreviewResult.Text textResult:
                    ActivePreviewType = PreviewerToolPreviewType.Text;
                    ActiveDocument = new TextDocument(textResult.Content);
                    break;

                case PreviewResult.Image imgResult:
                    ActivePreviewType = PreviewerToolPreviewType.Image;
                    ImagePreview.UpdateImage(imgResult.Bitmap, (TextureFormat?)imgResult.Format);
                    break;

                case PreviewResult.Mesh meshResult:
                    ActivePreviewType = PreviewerToolPreviewType.Mesh;
                    ActiveMesh = meshResult.MeshObject;
                    break;

                case PreviewResult.Error errResult:
                    SetDisplayText(errResult.Message);
                    break;
                case PreviewResult.Font fontResult:
                    ActivePreviewType = PreviewerToolPreviewType.Font;
                    FontPreview.SetFontData(fontResult.GlyphPages);
                    break;
                default:
                    SetDisplayText($"Unsupported preview result type: {result?.GetType().Name}");
                    break;
            }
        }
        catch (Exception ex)
        {
            SetDisplayText($"Error generating preview: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void SetDisplayNone()
    {
        ActivePreviewType = PreviewerToolPreviewType.None;
        ActiveDocument = null;
    }

    private void SetDisplayText(string text)
    {
        ActivePreviewType = PreviewerToolPreviewType.Text;
        ActiveDocument = new TextDocument(text);
    }
}

public enum PreviewerToolPreviewType
{
    None,
    Image,
    Text,
    Mesh,
    Font
}
