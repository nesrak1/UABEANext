using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic.ImportExport;
using UABEANext4.Plugins;
using TexturePlugin.Helpers; // Используем хелперы из плагина текстур
using AssetsTools.NET.Texture;

namespace UABEANext4.ViewModels.Documents;

public partial class AssetDiffDocumentViewModel : Document
{
    private readonly Workspace _workspace;
    
    [ObservableProperty]
    private ObservableCollection<DiffAssetItem> _diffItems = new();

    [ObservableProperty]
    private DiffAssetItem? _selectedDiffItem;

    // Свойства для предпросмотра
    [ObservableProperty] private Bitmap? _leftPreviewImage;
    [ObservableProperty] private Bitmap? _rightPreviewImage;
    [ObservableProperty] private string _previewText = "Select a Texture2D to compare.";
    [ObservableProperty] private bool _isImagePreviewVisible;

    public AssetDiffDocumentViewModel(Workspace workspace, string title)
    {
        _workspace = workspace;
        Title = title;
        Id = "AssetDiff";
    }

    public void LoadDiffs(System.Collections.Generic.List<DiffAssetItem> items)
    {
        DiffItems = new ObservableCollection<DiffAssetItem>(items);
    }

    partial void OnSelectedDiffItemChanged(DiffAssetItem? value)
    {
        if (value == null) return;

        // Очистка предыдущего
        LeftPreviewImage = null;
        RightPreviewImage = null;
        IsImagePreviewVisible = false;
        PreviewText = "No preview available for this type.";

        // Логика предпросмотра (фокус на Texture2D, как в запросе)
        var assetType = value.LeftAsset?.Type ?? value.RightAsset?.Type;

        if (assetType == AssetsTools.NET.Extra.AssetClassID.Texture2D || 
            assetType == AssetsTools.NET.Extra.AssetClassID.Sprite)
        {
            GenerateTextureComparison(value);
        }
        else
        {
            PreviewText = $"Comparison for {assetType} not implemented yet.\n" +
                          $"Left Size: {value.LeftAsset?.ByteSize ?? 0}\n" +
                          $"Right Size: {value.RightAsset?.ByteSize ?? 0}";
        }
    }

    private void GenerateTextureComparison(DiffAssetItem item)
    {
        IsImagePreviewVisible = true;
        PreviewText = "";

        if (item.LeftAsset != null)
        {
            try
            {
                LeftPreviewImage = TextureLoader.GetTexture2DBitmap(_workspace, item.LeftAsset, out _);
            }
            catch { /* Ignored */ }
        }

        if (item.RightAsset != null)
        {
            try
            {
                RightPreviewImage = TextureLoader.GetTexture2DBitmap(_workspace, item.RightAsset, out _);
            }
            catch { /* Ignored */ }
        }
    }
}
