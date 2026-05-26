using System.ComponentModel;
using System.Globalization;
using System.Resources;
using UABEANext4.Assets.Localization;

namespace UABEANext4.Services;

public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private readonly ResourceManager _resourceManager = Localization.ResourceManager;

    public void SetLanguage(string culture)
    {
        CultureInfo target = new(culture);

        CultureInfo.CurrentUICulture = target;
        CultureInfo.CurrentCulture = target;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}