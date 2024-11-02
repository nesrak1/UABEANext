using AssetsTools.NET;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using static UABEANext4.Themes.TypeHighlightingBrushes;

namespace UABEANext4.Logic.AssetInfo;

public class FlatListToTreeConverter : IValueConverter
{
    private static InlineCollection GenerateInlines(TypeTreeType type, TypeTreeNode node)
    {
        var inlines = new InlineCollection();

        var typeName = node.GetTypeString(type.StringBufferBytes);
        var fieldName = node.GetNameString(type.StringBufferBytes);
        var isValueType = AssetTypeValueField.GetValueTypeByTypeName(typeName) != AssetValueType.None;

        var span1 = new Span()
        {
            Foreground = isValueType
                ? ValueBrush
                : TypeNameBrush
        };
        {
            var bold = new Bold();
            {
                bold.Inlines.Add(typeName);
            }
            span1.Inlines.Add(bold);
        }
        span1.Inlines.Add(" ");
        inlines.Add(span1);

        var span2 = new Span();
        {
            var bold = new Bold();
            {
                bold.Inlines.Add(fieldName);
            }
            span2.Inlines.Add(bold);
        }
        inlines.Add(span2);

        return inlines;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rootNodes = new ObservableCollection<TypeTreeUINode>();
        if (value is null)
        {
            // if selected typetree type info is null, return an list
            return rootNodes;
        }

        if (value is not TypeTreeTypeInfo typeTreeTypeInfo)
        {
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
        }

        var typeTreeType = typeTreeTypeInfo.TtType;
        var flatList = typeTreeType.Nodes;

        var lookup = new Dictionary<int, TypeTreeUINode>();

        foreach (var item in flatList)
        {
            var uiNode = new TypeTreeUINode
            {
                Node = item,
                Display = GenerateInlines(typeTreeType, item),
                Children = new List<TypeTreeUINode>()
            };

            if (item.Level == 0)
            {
                rootNodes.Add(uiNode);
            }
            else
            {
                if (lookup.TryGetValue(item.Level - 1, out var parentNode))
                {
                    parentNode.Children.Add(uiNode);
                }
                else
                {
                    // hopefully this doesn't happen
                }
            }
            lookup[item.Level] = uiNode;
        }

        return rootNodes;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }
}
