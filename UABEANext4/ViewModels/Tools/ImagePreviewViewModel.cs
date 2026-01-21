using AssetsTools.NET.Texture;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace UABEANext4.ViewModels.Tools;
public partial class ImagePreviewViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _image;

    [ObservableProperty]
    private string _imageInfo = "No image";

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private string _textureFormat = string.Empty;
    [ObservableProperty]
    private string _currentBgLabel = "A";

    private readonly string[] _bgLabels = ["A", "C", "B", "W", "G"];
    public double ZoomLevelY => -ZoomLevel;
    public double DisplayWidth => Image != null ? Image.PixelSize.Width * ZoomLevel : 0;
    public double DisplayHeight => Image != null ? Image.PixelSize.Height * ZoomLevel : 0;
    public double RenderScaleY => -1.0;

    [ObservableProperty]
    private IBrush _previewBackground = Brushes.Transparent;

    private int _bgIndex = 0;
    private readonly IBrush[] _backgrounds = [
       Brushes.Transparent,
        // Checkerboard pattern
        new DrawingBrush
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(0, 0, 20, 20, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 20, 20, RelativeUnit.Absolute),
            Drawing = new GeometryDrawing
            {
                Brush = Brushes.LightGray,
                Geometry = Geometry.Parse("M0,0 H10 V10 H0 Z M10,10 H20 V20 H10 Z")
            }
        },
        Brushes.Black,
        Brushes.White,
        Brushes.Gray
    ];


    public void UpdateImage(Bitmap? bitmap, TextureFormat? textureFormat)
    {
        if (Image != null) {
            Image.Dispose();
        }

        Image = bitmap;
        if (bitmap != null)
        {
            ImageInfo = $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height} px";
            if (textureFormat.HasValue)
            {
                TextureFormat = $"Format: {textureFormat.Value}";
            }
            else
            {
                TextureFormat = "Unknown Format";
            }
        }
        else
        {
            ImageInfo = "No image";
            ZoomLevel = 1.0;
            OnPropertyChanged(nameof(ZoomLevelY));
        }
    }

    public void FitToSize(double availableWidth, double availableHeight)
    {
        if (Image == null || availableWidth <= 0 || availableHeight <= 0)
            return;

        double ratioX = availableWidth / Image.PixelSize.Width;
        double ratioY = availableHeight / Image.PixelSize.Height;

        ZoomLevel = Math.Min(ratioX, ratioY) * 0.95;

        if (ZoomLevel > 1.0)
            ZoomLevel = 1.0;
    }

    [RelayCommand]
    private void CycleBackground()
    {
        _bgIndex = (_bgIndex + 1) % _backgrounds.Length;
        PreviewBackground = _backgrounds[_bgIndex];
        CurrentBgLabel = _bgLabels[_bgIndex];
    }

    public void AdjustZoom(bool increase)
    {
        double step = 1.1;
        if (increase)
            ZoomLevel *= step;
        else
            ZoomLevel /= step;

        ZoomLevel = Math.Clamp(ZoomLevel, 0.05, 20.0);
        OnPropertyChanged(nameof(ZoomLevelY));
    }

    partial void OnZoomLevelChanged(double value)
    {
        OnPropertyChanged(nameof(DisplayWidth));
        OnPropertyChanged(nameof(DisplayHeight));
    }

}
