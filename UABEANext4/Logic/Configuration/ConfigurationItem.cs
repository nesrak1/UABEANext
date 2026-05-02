using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using UABEANext4.Util;

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
        string titleKey = $"Config.Title.{property.Name}";
        string descKey = $"Config.Desc.{property.Name}";

        return (
            LocalizationHelper.GetString(titleKey, titleAttr.Title),
            LocalizationHelper.GetString(descKey, descAttr?.Description ?? "")
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
    private readonly Dictionary<string, string> _nameToDescription = new();
    private readonly Dictionary<string, string> _descriptionToName = new();
    
    [ObservableProperty] private IReadOnlyList<string> _enumValues;

    public ConfigurationEnumItem(PropertyInfo property)
    {   
        _property = property;
        _enumType = property.PropertyType;

        var names = Enum.GetNames(_enumType);
        foreach (var name in names)
        {
            var fieldInfo = _enumType.GetField(name);
            var descAttr = fieldInfo?.GetCustomAttribute<DescriptionAttribute>();
            var desc = descAttr?.Description ?? name;
            
            _nameToDescription[name] = desc;
            _descriptionToName[desc] = name;
        }

        _enumValues = _nameToDescription.Values.ToList().AsReadOnly();
        
        var (title, descTitle) = GetBaseAttrs(property);
        Title = title;
        Description = descTitle ?? "No description.";
        
        ConfigurationManager.Settings.PropertyChanged += (s, e) => {
            if (e.PropertyName == _property.Name)
            {
                OnPropertyChanged(nameof(Value));
            }
        };
    }

    public string Value
    {
        get
        {
            var enumVal = _property.GetValue(ConfigurationManager.Settings);
            var name = Enum.GetName(_enumType, enumVal!);
            return name != null && _nameToDescription.TryGetValue(name, out var desc) 
                ? desc 
                : "Unknown enum value";
        }
        set
        {
            var name = _descriptionToName.TryGetValue(value, out var matchedName) ? matchedName : value;

            if (Enum.TryParse(_enumType, name, out var enumValue))
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