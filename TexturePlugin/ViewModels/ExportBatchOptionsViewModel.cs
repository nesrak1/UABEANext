using AssetsTools.NET.Texture;
using CommunityToolkit.Mvvm.ComponentModel;
using UABEANext4.Interfaces;
using UABEANext4.ViewModels;

namespace TexturePlugin.ViewModels;
public partial class ExportBatchOptionsViewModel : ViewModelBase, IDialogAware<ExportBatchOptionsResult?>
{
    [ObservableProperty]
    public ImageExportType _selectedExportType;
    [ObservableProperty]
    public int _quality;

    public List<string> DropdownItems { get; }

    public string Title => "Texture Batch Export";
    public int Width => 300;
    public int Height => 130;
    public event Action<ExportBatchOptionsResult?>? RequestClose;

    public ExportBatchOptionsViewModel()
    {
        SelectedExportType = ImageExportType.Png;
        Quality = 100;

        DropdownItems =
        [
            "BMP (alpha, uncompressed, lossless)",
            "PNG (alpha, compressed, lossless)",
            "JPG (no alpha, compressed, lossy)",
            "TGA (alpha, compressed, lossless)"
        ];
    }

    public void BtnOk_Click()
    {
        RequestClose?.Invoke(new ExportBatchOptionsResult(SelectedExportType, Quality));
    }

    public void BtnCancel_Click()
    {
        RequestClose?.Invoke(null);
    }
}

public readonly struct ExportBatchOptionsResult
{
    public ImageExportType ImageType { get; }
    public int Quality { get; }

    public readonly string Extension => ImageType switch
    {
        ImageExportType.Bmp => ".bmp",
        ImageExportType.Png => ".png",
        ImageExportType.Jpg => ".jpg",
        ImageExportType.Tga => ".tga",
        _ => throw new ArgumentOutOfRangeException(nameof(ImageType))
    };

    public ExportBatchOptionsResult(ImageExportType imageType, int quality)
    {
        ImageType = imageType;
        Quality = quality;
    }
}
