using System;
using System.Collections.Generic;

namespace UABEANext4.Util;
public static class GeneralExtensionUtils
{
    // https://stackoverflow.com/a/50481101
    public static IEnumerable<T> GetUniqueFlags<T>(this T flags) where T : Enum
    {
        foreach (Enum value in Enum.GetValues(flags.GetType()))
            if (flags.HasFlag(value))
                yield return (T)value;
    }
}
