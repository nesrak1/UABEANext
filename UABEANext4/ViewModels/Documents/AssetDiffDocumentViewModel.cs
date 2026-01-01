using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.ImportExport;
using UABEANext4.Plugins;

namespace UABEANext4.ViewModels.Documents;

public partial class AssetDiffDocumentViewModel : Document
{
    private readonly Workspace _workspace;
    private readonly UavPluginFunctions _pluginFuncs = new UavPluginFunctions();
    
    [ObservableProperty] private ObservableCollection<DiffAssetItem> _diffItems = new();
    [ObservableProperty] private DiffAssetItem? _selectedDiffItem;
    [ObservableProperty] private Bitmap? _leftPreviewImage;
    [ObservableProperty] private Bitmap? _rightPreviewImage;
    [ObservableProperty] private string _previewText = "Select an item to see differences.";
    [ObservableProperty] private bool _isImagePreviewVisible;

    public AssetDiffDocumentViewModel(Workspace workspace, string title)
    {
        _workspace = workspace;
        Title = title;
        Id = "AssetDiff_" + System.Guid.NewGuid().ToString();
    }

    public void LoadDiffs(List<DiffAssetItem> items)
    {
        DiffItems = new ObservableCollection<DiffAssetItem>(items);
    }

    partial void OnSelectedDiffItemChanged(DiffAssetItem? value)
    {
        if (value == null) return;

        if (LeftPreviewImage != null) { LeftPreviewImage.Dispose(); LeftPreviewImage = null; }
        if (RightPreviewImage != null) { RightPreviewImage.Dispose(); RightPreviewImage = null; }
        IsImagePreviewVisible = false;
        
        var asset = value.LeftAsset ?? value.RightAsset;
        if (asset == null) return;

        var previewers = _workspace.Plugins.GetPreviewersThatSupport(_workspace, asset);
        var imagePreviewer = previewers.FirstOrDefault(p => p.PreviewType == UavPluginPreviewerType.Image);

        if (imagePreviewer != null)
        {
            IsImagePreviewVisible = true;
            PreviewText = "";
            if (value.LeftAsset != null)
                LeftPreviewImage = imagePreviewer.Previewer.ExecuteImage(_workspace, _pluginFuncs, value.LeftAsset, out _);
            if (value.RightAsset != null)
                RightPreviewImage = imagePreviewer.Previewer.ExecuteImage(_workspace, _pluginFuncs, value.RightAsset, out _);
        }
        else
        {
            PreviewText = $"Type: {value.Type}\nPath ID: {value.PathId}\nStatus: {value.Status}\n\n" +
                          $"Left Size: {(value.LeftAsset?.ByteSize.ToString() ?? "N/A")} bytes\n" +
                          $"Right Size: {(value.RightAsset?.ByteSize.ToString() ?? "N/A")} bytes";
        }
    }
}
