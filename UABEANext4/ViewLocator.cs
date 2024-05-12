using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Core;
using System;

namespace UABEANext4;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data == null)
        {
            return new TextBlock { Text = "Null view model" };
        }

        var dataType = data.GetType();
        var name = dataType.FullName!.Replace("ViewModel", "View");
        var type = dataType.Assembly.GetType(name);

        if (type != null)
        {
            var instance = (Control)Activator.CreateInstance(type)!;
            if (instance != null)
            {
                return instance;
            }
            else
            {
                return new TextBlock { Text = "Create Instance Failed: " + type.FullName };
            }
        }
        else
        {
            return new TextBlock { Text = "Not Found: " + name };
        }
    }

    public bool Match(object? data)
    {
        return data is ObservableObject or IDockable;
    }
}
