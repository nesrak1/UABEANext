using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace UABEANext4.Logic.Configuration;
public abstract class ConfigurationItemBase : ObservableObject
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    protected static (string, string?) GetBaseAttrs(PropertyInfo property)
    {
        var titleAttr = property.GetCustomAttribute<ConfigTitle>();
        var descAttr = property.GetCustomAttribute<ConfigDesc>();
        if (titleAttr is null)
        {
            throw new Exception("Missing title for settings property");
        }
        
        return (
            titleAttr.Title,
            descAttr?.Description
        );
    }
}

public partial class ConfigurationIntegerItem : ConfigurationItemBase
{
    private readonly PropertyInfo _property;

    [ObservableProperty] private int? _rangeMin;
    [ObservableProperty] private int? _rangeMax;

    public ConfigurationIntegerItem(PropertyInfo property)
    {
        _property = property;

        var (title, desc) = GetBaseAttrs(property);
        Title = title;
        Description = desc ?? "No description.";
        
        var range = property.GetCustomAttribute<ConfigRange>();
        RangeMin = range?.Minimum;
        RangeMax = range?.Maximum;
        
        ConfigurationManager.Settings.PropertyChanged += (s, e) => {
            if (e.PropertyName == _property.Name)
            {
                OnPropertyChanged(nameof(Value));
            }
        };
    }

    public int Value
    {
        get => (int)_property.GetValue(ConfigurationManager.Settings)!;
        set => _property.SetValue(ConfigurationManager.Settings, value);
    }
}

public class ConfigurationBooleanItem : ConfigurationItemBase
{
    private readonly PropertyInfo _property;

    public ConfigurationBooleanItem(PropertyInfo property)
    {
        _property = property;

        var (title, desc) = GetBaseAttrs(property);
        Title = title;
        Description = desc ?? "No description.";
        
        ConfigurationManager.Settings.PropertyChanged += (s, e) => {
            if (e.PropertyName == _property.Name)
            {
                OnPropertyChanged(nameof(Value));
            }
        };
    }

    public bool Value
    {
        get => (bool)_property.GetValue(ConfigurationManager.Settings)!;
        set => _property.SetValue(ConfigurationManager.Settings, value);
    }
}

public partial class ConfigurationEnumItem : ConfigurationItemBase
{
    private readonly PropertyInfo _property;
    private readonly Type _enumType;
    
    [ObservableProperty] private IReadOnlyList<string> _enumValues;

    public ConfigurationEnumItem(PropertyInfo property)
    {   
        _property = property;
        _enumType = property.PropertyType;
        _enumValues = Enum.GetNames(_enumType).ToList().AsReadOnly();
        
        var (title, desc) = GetBaseAttrs(property);
        Title = title;
        Description = desc ?? "No description.";
        
        ConfigurationManager.Settings.PropertyChanged += (s, e) => {
            if (e.PropertyName == _property.Name)
            {
                OnPropertyChanged(nameof(Value));
            }
        };
    }

    public string Value
    {
        get => Enum.GetName(_enumType, _property.GetValue(ConfigurationManager.Settings)!)
            ?? "Unknown enum value";
        set
        {
            if (Enum.TryParse(_enumType, value, out var enumValue))
            {
                _property.SetValue(ConfigurationManager.Settings, enumValue);
            }
            else
            {
                var zeroValue = Enum.GetValues(_enumType).GetValue(0);
                _property.SetValue(ConfigurationManager.Settings, zeroValue);
            }
        }
    }
}