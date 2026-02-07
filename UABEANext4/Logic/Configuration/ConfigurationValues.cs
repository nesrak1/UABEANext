using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using UABEANext4.Util;

namespace UABEANext4.Logic.Configuration;

public partial class ConfigurationValues : ObservableObject
{
    [ObservableProperty]
    [property: ConfigTitle("Theme Type")]
    [property: ConfigDesc("The theme to use.")]
    private ConfigurationThemeType _themeType = ConfigurationThemeType.Auto;

    [ObservableProperty]
    [property: ConfigTitle("Use Managed over IL2CPP")]
    [property: ConfigDesc("Use the Managed folder if it exists, rather than use CPP2IL.")]
    private bool _useManagedOverIl2cpp = false;

    [ObservableProperty]
    [property: ConfigTitle("Listing Filename Length Limit")]
    [property: ConfigDesc("Maximum length for the asset name when generating asset list.")]
    [property: ConfigRange(0, int.MaxValue)]
    private int _listingNameLength = 300;

    [ObservableProperty]
    [property: ConfigTitle("Export Filename Length Limit")]
    [property: ConfigDesc("Maximum length for the asset name when exporting assets.")]
    [property: ConfigRange(0, int.MaxValue)]
    private int _exportNameLength = 150;

    [ObservableProperty]
    [property: ConfigTitle("Load Container Paths")]
    [property: ConfigDesc("Load container paths, which may take a while when loading many assets.")]
    private bool _loadContainerPaths = true;

    private readonly Action<int> _saveDebounceFunc = DebounceUtils.Debounce(
        (int _) => ConfigurationManager.SaveConfig(), 500);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // special case: theme updates immediately
        if (e.PropertyName == nameof(ThemeType) && Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = ThemeType switch
            {
                ConfigurationThemeType.Auto => ThemeVariant.Default,
                ConfigurationThemeType.Light => ThemeVariant.Light,
                ConfigurationThemeType.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default // shouldn't happen
            };
        }

        _saveDebounceFunc(0);
    }
}

public class ConfigTitle(string title) : Attribute
{
    public string Title { get; } = title;
}

public class ConfigDesc(string description) : Attribute
{
    public string Description { get; } = description;
}

public class ConfigRange(int min, int max) : Attribute
{
    public int Minimum { get; } = min;
    public int Maximum { get; } = max;
}
