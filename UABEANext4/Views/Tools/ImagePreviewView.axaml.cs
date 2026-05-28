using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.ComponentModel;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;

public partial class ImagePreviewView : UserControl
{
    private bool _pointerDown = false;
    private Point _lastPointerPos = new Point(-1, -1);

    public ImagePreviewView()
    {
        InitializeComponent();

        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;

        DataContextChanged += (s, e) =>
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

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pointerDown = true;
        _lastPointerPos = e.GetPosition(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pointerDown)
        {
            var curPointerPos = e.GetPosition(this);
            var dragOffset = curPointerPos - _lastPointerPos;
            ImageScroll.Offset += new Vector(-dragOffset.X, -dragOffset.Y);
            _lastPointerPos = curPointerPos;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pointerDown = false;
        _lastPointerPos = new Point(-1, -1);
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