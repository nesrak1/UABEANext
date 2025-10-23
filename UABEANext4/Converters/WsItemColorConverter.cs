using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using UABEANext4.AssetWorkspace;

namespace UABEANext4.Converters;
public class WsItemColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values?.Count != 2 || !targetType.IsAssignableFrom(typeof(IBrush)))
            throw new NotSupportedException();

        if (values[0] is not WorkspaceItemType type)
            return BindingOperations.DoNothing;

        return type switch
        {
            WorkspaceItemType.BundleFile => GetBrushFromName("WorkspaceItemBundleBrush"),
            WorkspaceItemType.AssetsFile => GetBrushFromName("WorkspaceItemAssetsBrush"),
            WorkspaceItemType.ResourceFile => GetBrushFromName("WorkspaceItemResourceBrush"),
            WorkspaceItemType.OtherFile => GetBrushFromName("WorkspaceItemOtherBrush"),
            _ => GetBrushFromName("WorkspaceItemEtcBrush"),
        };
    }

    private static ISolidColorBrush GetBrushFromName(string key)
    {
        var currentApp = Application.Current;
        if (currentApp is null)
            return new SolidColorBrush(Colors.Black);

        if (!currentApp.TryFindResource(key, currentApp.ActualThemeVariant, out object? value))
            return new SolidColorBrush(Colors.Black);

        if (value is not ISolidColorBrush brush)
            return new SolidColorBrush(Colors.Black);

        return brush;
    }
}