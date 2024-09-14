using AssetsTools.NET.Extra;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace UABEANext4.Converters;
public class AssetsFileInstanceNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AssetsFileInstance fileInst)
        {
            return fileInst.name;
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
