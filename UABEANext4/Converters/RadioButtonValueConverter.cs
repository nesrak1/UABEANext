using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System;
using System.Globalization;

namespace UABEANext4.Converters;
public class RadioButtonValueConverter : MarkupExtension, IValueConverter
{
    public RadioButtonValueConverter(object optionValue)
        => OptionValue = optionValue;

    public object OptionValue { get; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.Equals(OptionValue);

    public object? ConvertBack(object? isChecked, Type targetType, object? parameter, CultureInfo culture)
        => (bool)(isChecked ?? false)
            ? OptionValue
            : null;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;
}