using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;
using UABEANext4.Logic.Configuration;

namespace UABEANext4.Util;

public static class LocalizationHelper
{
    private const string LocalizationUriTemplate = "avares://UABEANext4/Assets/Localization/{0}.axaml";

    private static readonly string[] SupportedLocales =
    [
        "en-US",
        "es-ES"
    ];

    public static string GetLocale(ConfigurationLanguage language) => language switch
    {
        ConfigurationLanguage.EsES => "es-ES",
        _ => "en-US"
    };

    public static void ApplyLanguage(ConfigurationLanguage language)
    {
        var locale = GetLocale(language);
        var uri = new Uri(string.Format(LocalizationUriTemplate, locale));

        if (Application.Current is null)
            return;

        var mergedDicts = Application.Current.Resources.MergedDictionaries;

        // Remove any existing localization dictionary
        var existing = mergedDicts
            .OfType<ResourceDictionary>()
            .Where(d => d.Count > 0 && SupportedLocales.Any(loc =>
                d.ContainsKey("Menu.File"))) // sentinel key present in all locale files
            .ToList();

        foreach (var old in existing)
            mergedDicts.Remove(old);

        // Load and add the new locale dictionary
        var newDict = (ResourceDictionary)AvaloniaXamlLoader.Load(uri);
        mergedDicts.Add(newDict);
    }

    public static string GetString(string key, string fallback = "")
    {
        if (Application.Current != null && Application.Current.TryFindResource(key, out var resource) && resource is string s)
        {
            return s;
        }
        return fallback;
    }
}
