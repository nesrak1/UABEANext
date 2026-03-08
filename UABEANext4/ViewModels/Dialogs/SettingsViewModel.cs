using AssetsTools.NET;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UABEANext4.Interfaces;
using UABEANext4.Logic.Configuration;
using UABEANext4.Logic.ImportExport;
using UABEANext4.Util;

namespace UABEANext4.ViewModels.Dialogs;
public partial class SettingsViewModel : ViewModelBase, IDialogAware
{
    [ObservableProperty]
    private ObservableCollection<ConfigurationItemBase> _configItems = [];
    
    public string Title => "Settings";
    public int Width => 350;
    public int Height => 550;
    public bool IsModal => true;

    public SettingsViewModel()
    {
        var properties = typeof(ConfigurationValues)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var propType = property.PropertyType;
            if (propType == typeof(bool))
            {
                ConfigItems.Add(new ConfigurationBooleanItem(property));
            }
            else if (propType == typeof(int))
            {
                ConfigItems.Add(new ConfigurationIntegerItem(property));
            }
            else if (propType.IsEnum)
            {
                ConfigItems.Add(new ConfigurationEnumItem(property));
            }
        }
    }
}
