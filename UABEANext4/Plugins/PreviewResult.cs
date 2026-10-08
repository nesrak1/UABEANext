
using AssetsTools.NET.Texture;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Plugins;

public record GlyphInfo (int StartCodepoint, int EndCodepoint, Bitmap Glyphs);

public abstract record PreviewResult
{
    public record Text(string Content) : PreviewResult;
    public record Image(Bitmap Bitmap, int Format) : PreviewResult;
    public record Mesh(MeshObj MeshObject) : PreviewResult;
    public record Error(string Message) : PreviewResult;

    public record Font(List<GlyphInfo> GlyphPages) : PreviewResult;
    private PreviewResult() { }
}
