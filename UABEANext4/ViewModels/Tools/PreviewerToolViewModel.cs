using Avalonia.Media.Imaging;
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
    public Bitmap? _activeImage;
    [ObservableProperty]
    public TextDocument? _activeDocument;
    [ObservableProperty]
    public MeshObj? _activeMesh;
    [ObservableProperty]
    public PreviewerToolPreviewType _activePreviewType = PreviewerToolPreviewType.Text;

    // defer this to first preview since dialogs won't exist until after initial load
    private readonly Lazy<UavPluginFunctions> _uavPluginFuncs = new(() => new UavPluginFunctions());

    [Obsolete("This constructor is for the designer only and should not be used directly.", true)]
    public PreviewerToolViewModel()
    {
        Workspace = new();

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeImage = null;
        _activeDocument = new TextDocument();
        _activeMesh = new MeshObj();
    }

    public PreviewerToolViewModel(Workspace workspace)
    {
        Workspace = workspace;

        Id = TOOL_TITLE.Replace(" ", "");
        Title = TOOL_TITLE;

        _activeImage = null;
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
            SetDisplayText(string.Empty);
            return;
        }

        var pluginsList = Workspace.Plugins.GetPreviewersThatSupport(Workspace, asset);
        if (pluginsList == null || pluginsList.Count == 0)
        {
            SetDisplayText("No preview available.");
            return;
        }

        var firstPrevPair = pluginsList[0];
        var prevType = firstPrevPair.PreviewType;
        var prev = firstPrevPair.Previewer;

        switch (prevType)
        {
            case UavPluginPreviewerType.Image:
            {
                ActivePreviewType = PreviewerToolPreviewType.Image;
                DisposeCurrentImage();

                var image = prev.ExecuteImage(Workspace, _uavPluginFuncs.Value, asset, out string? error);
                if (image != null)
                {
                    ActiveImage = image;
                }
                else
                {
                    SetDisplayText(error ?? "[null error]");
                }
                break;
            }
            case UavPluginPreviewerType.Text:
            {
                ActivePreviewType = PreviewerToolPreviewType.Text;

                var textString = prev.ExecuteText(Workspace, _uavPluginFuncs.Value, asset, out string? error);
                if (textString != null)
                {
                    ActiveDocument = new TextDocument(textString);
                }
                else
                {
                    SetDisplayText(error ?? "[null error]");
                }
                break;
            }
            case UavPluginPreviewerType.Mesh:
            {
                ActivePreviewType = PreviewerToolPreviewType.Mesh;

                var meshObj = prev.ExecuteMesh(Workspace, _uavPluginFuncs.Value, asset, out string? error);
                if (meshObj != null)
                {
                    ActiveMesh = meshObj;
                }
                else
                {
                    SetDisplayText(error ?? "[null error]");
                }
                break;
            }
            default:
            {
                SetDisplayText($"Preview type {prevType} not supported.");
                break;
            }
        }
    }

    private void SetDisplayText(string text)
    {
        ActivePreviewType = PreviewerToolPreviewType.Text;
        ActiveDocument = new TextDocument(text);
    }

    private void DisposeCurrentImage()
    {
        if (ActiveImage != null)
        {
            ActiveImage.Dispose();
            ActiveImage = null;
        }
    }
}

public enum PreviewerToolPreviewType
{
    Image,
    Text,
    Mesh,
}
