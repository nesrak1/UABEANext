using Avalonia.Media;

namespace UABEANext4.Themes;
public static class TypeHighlightingBrushes
{
    public static readonly SolidColorBrush PrimNameBrushDark = SolidColorBrush.Parse("#569cd6");
    public static readonly SolidColorBrush PrimNameBrushLight = SolidColorBrush.Parse("#0000ff");
    public static readonly SolidColorBrush TypeNameBrushDark = SolidColorBrush.Parse("#4ec9b0");
    public static readonly SolidColorBrush TypeNameBrushLight = SolidColorBrush.Parse("#2b91af");
    public static readonly SolidColorBrush StringBrushDark = SolidColorBrush.Parse("#d69d85");
    public static readonly SolidColorBrush StringBrushLight = SolidColorBrush.Parse("#a31515");
    public static readonly SolidColorBrush ValueBrushDark = SolidColorBrush.Parse("#b5cea8");
    public static readonly SolidColorBrush ValueBrushLight = SolidColorBrush.Parse("#5b2da8");

    public static SolidColorBrush PrimNameBrush
    {
        get
        {
            return true
                ? PrimNameBrushDark
                : PrimNameBrushLight;
        }
    }

    public static SolidColorBrush TypeNameBrush
    {
        get
        {
            return true
                ? TypeNameBrushDark
                : TypeNameBrushLight;
        }
    }

    public static SolidColorBrush StringBrush
    {
        get
        {
            return true
                ? StringBrushDark
                : StringBrushLight;
        }
    }

    public static SolidColorBrush ValueBrush
    {
        get
        {
            return true
                ? ValueBrushDark
                : ValueBrushLight;
        }
    }
}
