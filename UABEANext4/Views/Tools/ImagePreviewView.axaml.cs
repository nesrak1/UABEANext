using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.ComponentModel;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;

public partial class ImagePreviewView : UserControl
{
    public ImagePreviewView()
    {
        InitializeComponent();
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);

        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is PreviewerToolViewModel vm)
            {
                vm.ImagePreview.PropertyChanged += OnImagePreviewPropertyChanged;
            }
        };
    }

    private void OnImagePreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImagePreviewViewModel.Image))
        {
            TriggerFit();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (DataContext is PreviewerToolViewModel vm)
            {
                vm.ImagePreview.AdjustZoom(e.Delta.Y > 0);
                e.Handled = true;
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        TriggerFit();
    }

    private void TriggerFit()
    {
        if (DataContext is PreviewerToolViewModel vm)
        {
            if (ImageScroll != null)
            {
                vm.ImagePreview.FitToSize(ImageScroll.Bounds.Width, ImageScroll.Bounds.Height);
            }
        }
    }
}