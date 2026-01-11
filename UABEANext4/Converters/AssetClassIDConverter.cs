using AssetsTools.NET.Extra;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UABEANext4.Converters;

public class AssetClassIDConverter : IValueConverter
{
    private Dictionary<AssetClassID, string> _nameLookup = Enum
        .GetValues(typeof(AssetClassID))
        .Cast<AssetClassID>()
        .ToDictionary(enm => enm, enm => enm.ToString());

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AssetClassID classId)
        {
            if (_nameLookup.TryGetValue(classId, out string? name))
                return name;

            if ((int)classId < 0)
                return _nameLookup[AssetClassID.MonoBehaviour];

            return ((int)classId).ToString();
        }

        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
