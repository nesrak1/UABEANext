using System;

namespace UABEANext4.Plugins;

[Flags]
public enum UavPluginMode
{
    Import = 1,
    Export = 2,
    Console = 4,
    Create = 8,
    All = 15
}