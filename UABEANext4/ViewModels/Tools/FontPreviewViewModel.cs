using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.Plugins;

namespace UABEANext4.ViewModels.Tools;

public partial class FontPreviewViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap? _image;

    [ObservableProperty]
    private List<GlyphInfo>? _glyphPages;
    [ObservableProperty]
    private List<string>? _pageNames;

    [ObservableProperty]
    private int _currentPageIndex = -1;

    partial void OnCurrentPageIndexChanged(int value)
    {
        if (GlyphPages != null && value >= 0 && value < GlyphPages.Count)
        {
            Image = GlyphPages[value].Glyphs;
        }
    }

    public void SetFontData(List<GlyphInfo> pages)
    {
        GlyphPages = pages;
        PageNames = pages.Select(p => $"U+{p.StartCodepoint:X4} - U+{p.EndCodepoint:X4}").ToList();
        CurrentPageIndex = 0;
    }
}

