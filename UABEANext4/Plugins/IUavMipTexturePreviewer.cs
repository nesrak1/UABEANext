using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Plugins;

public interface IUavMipTexturePreviewer
{
    int GetMipCount(Workspace workspace, AssetInst asset);
    IReadOnlyList<MipmapPreview> ExecuteMipmaps(Workspace workspace, AssetInst asset);
}

public sealed class MipmapPreview(int level, Bitmap image) : IDisposable
{
    public Bitmap Image { get; } = image;
    public string Label => $"Mip {level} · {Image.PixelSize.Width} × {Image.PixelSize.Height}";
    public double Width => Image.PixelSize.Width * Math.Min(1.0, 160.0 / Math.Max(Image.PixelSize.Width, Image.PixelSize.Height));
    public double Height => Image.PixelSize.Height * Width / Image.PixelSize.Width;
    public void Dispose() => Image.Dispose();
}
