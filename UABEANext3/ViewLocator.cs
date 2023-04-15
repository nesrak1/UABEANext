using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dock.Model.Core;
using ReactiveUI;
using System;
using UABEANext3.ViewModels;

namespace UABEANext3
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            var name = data.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                var instance = (Control)Activator.CreateInstance(type)!;
                if (instance != null)
                {
                    return (Control)instance;
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

        public bool Match(object data)
        {
            return data is ReactiveObject || data is IDockable;
        }
    }
}
