using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Reflection;
using UABEANext4.AssetWorkspace;
using UABEANext4.Plugins;

namespace FontPlugin;

public class FontAssetPreviewer : IUavPluginPreviewer
{
    private static readonly Dictionary<string, IGlyphTypeface> _fontCache = new();
    private const int GLYPHS_PER_PAGE = 256; // 16x16 grid
    private const int COLUMNS = 16;
    private const int ROWS = 16;

    public string Name => "Preview Font";
    public string Description => "Preview Font - Browse glyphs by Unicode pages";

    public UavPluginPreviewerType SupportsPreview(Workspace workspace, AssetInst selection)
    {
        return selection.Type == AssetClassID.Font
            ? UavPluginPreviewerType.Font
            : UavPluginPreviewerType.None;
    }

    // This method attempts to load the font directly from the byte data using Avalonia's internal font loading mechanisms using reflection.
    // Works on Avalonia 11, on Avalonia 12 might need adjustments if internal APIs changed.
    private IGlyphTypeface? LoadTypefaceDirectly(byte[] data)
    {
        try
        {
            var fontManager = FontManager.Current;

            var platformImpl = (IFontManagerImpl)fontManager.GetType()
                .GetProperty("PlatformImpl", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.GetValue(fontManager)!;

            using var ms = new MemoryStream(data);

            var method = platformImpl.GetType().GetMethod("TryCreateGlyphTypeface",
                new[] { typeof(Stream), typeof(FontSimulations), typeof(IGlyphTypeface).MakeByRefType() });

            object?[] args = { ms, FontSimulations.None, null };
            var success = (bool)method!.Invoke(platformImpl, args)!;

            return success ? (IGlyphTypeface)args[2]! : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public PreviewResult Execute(Workspace workspace, IUavPluginFunctions funcs, AssetInst selection)
    {
        try
        {
            var fontBaseField = FontHelper.GetByteArrayFont(workspace, selection);
            byte[] byteData = fontBaseField["m_FontData.Array"].AsByteArray;
            if (byteData is null || byteData.Length == 0)
            {
                return new PreviewResult.Error("Failed to load m_FontData. Data is empty");
            }

            IGlyphTypeface? glyphTypeface = LoadTypefaceDirectly(byteData);
            if (glyphTypeface == null)
                return new PreviewResult.Error("Failed to load typeface");

            string cacheKey = $"{selection.PathId}_{selection.TypeId}";
            _fontCache[cacheKey] = glyphTypeface;

            double displaySize = 18;

            const int MAX_CODEPOINT = 0x1FFFF;
            var activePages = new List<(int pageIndex, List<(int codepoint, ushort glyphIndex)> glyphs)>();

            for (int page = 0; page <= MAX_CODEPOINT / GLYPHS_PER_PAGE; page++)
            {
                var pageGlyphs = new List<(int codepoint, ushort glyphIndex)>();
                uint startCodepoint = (uint)(page * GLYPHS_PER_PAGE);
                uint endCodepoint = startCodepoint + GLYPHS_PER_PAGE;

                for (uint cp = startCodepoint; cp < endCodepoint; cp++)
                {
                    if (glyphTypeface.TryGetGlyph(cp, out ushort glyphIndex))
                    {
                        if (glyphIndex != 0)
                        {
                            pageGlyphs.Add(((int)cp, glyphIndex));
                        }
                    }
                }

                if (pageGlyphs.Count > 0)
                {
                    activePages.Add((page, pageGlyphs));
                }
            }

            if (activePages.Count == 0)
            {
                return new PreviewResult.Error("The font does not contain any compatible Unicode characters in the scanned range.");
            }

            List<GlyphInfo> atlases = new();
            foreach (var pageData in activePages)
            {
                atlases.Add(RenderFontPage(glyphTypeface, displaySize, pageData.pageIndex, pageData.glyphs));
            }

            return new PreviewResult.Font(atlases);
        }
        catch (Exception ex)
        {
            return new PreviewResult.Error($"Font preview error: {ex.Message}");
        }
    }

    public GlyphInfo RenderFontPage(IGlyphTypeface typeface, double glyphSize, int pageIndex, List<(int codepoint, ushort glyphIndex)> pageGlyphs)
    {

        int startCodepoint = pageIndex * GLYPHS_PER_PAGE;
        int endCodepoint = startCodepoint + GLYPHS_PER_PAGE - 1;

        double cellSize = Math.Ceiling(glyphSize * 1.8);
        double padding = 20;

        int width = (int)(COLUMNS * cellSize + padding * 2);
        int height = (int)(ROWS * cellSize + padding * 2);

        var pixelSize = new PixelSize(width, height);
        var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96, 96));

        var glyphMap = pageGlyphs.ToDictionary(g => g.codepoint, g => g.glyphIndex);

        using (var ctx = bitmap.CreateDrawingContext())
        {

            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#1e1e1e")), null, new Rect(0, 0, width, height));

            var foreground = Brushes.White;
            var gridPen = new Pen(new SolidColorBrush(Color.Parse("#2d2d2d")), 1);

            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLUMNS; c++)
                {
                    int cellIndex = r * COLUMNS + c;
                    int codepoint = startCodepoint + cellIndex;

                    double x = padding + c * cellSize;
                    double y = padding + r * cellSize;

                    ctx.DrawRectangle(null, gridPen, new Rect(x, y, cellSize, cellSize));

                    if (glyphMap.TryGetValue(codepoint, out ushort glyphIndex))
                    {
                        double estimatedGlyphWidth = glyphSize * 0.5;
                        double glyphX = x + (cellSize - glyphSize) / 2;
                        double glyphY = y + (cellSize - glyphSize) / 2;

                        try
                        {
                            ushort[] indices = { glyphIndex };
                            using var run = new GlyphRun(typeface, glyphSize, ReadOnlyMemory<char>.Empty, indices);
                            var state = ctx.PushTransform(Matrix.CreateTranslation(glyphX, glyphY));
                            ctx.DrawGlyphRun(foreground, run);
                            state.Dispose();
                        }
                        catch
                        {
                            // Ignore rendering errors for individual glyphs, just leave the cell blank
                        }
                    }
                }
            }
        }

        return new GlyphInfo(startCodepoint, endCodepoint, bitmap);
    }


    public void Cleanup()
    {
        _fontCache.Clear();
    }
}