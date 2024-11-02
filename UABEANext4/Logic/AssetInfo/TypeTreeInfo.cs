using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls.Documents;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UABEANext4.AssetWorkspace;
using static UABEANext4.Themes.TypeHighlightingBrushes;

namespace UABEANext4.Logic.AssetInfo;

public partial class TypeTreeInfo : ObservableObject
{
    public ObservableCollection<TypeTreeUINode> TypeTreeNodes { get; set; } = [];
    public List<TypeTreeTypeInfo> TypeTreeTypeInfos { get; set; } = [];

    private TypeTreeTypeInfo? _selectedType;
    public TypeTreeTypeInfo? SelectedType
    {
        get => _selectedType;
        set
        {
            _selectedType = value;
            SelectedTypeChanged(value);
        }
    }

    private TypeTreeUINode? _selectedNode;
    public TypeTreeUINode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            _selectedNode = value;
            SelectedNodeChanged(value);
        }
    }

    [ObservableProperty] public string _selectedTypeName = "";
    [ObservableProperty] public string _selectedTypeId = "";
    [ObservableProperty] public string _selectedScriptId = "";
    [ObservableProperty] public string _selectedTypeHash = "";
    [ObservableProperty] public string _selectedMonoHash = "";
    [ObservableProperty] public string _selectedAligned = "";
    [ObservableProperty] public string _selectedTypeFlags = "";
    [ObservableProperty] public string _selectedMetaFlags = "";

    public TypeTreeInfo(Workspace workspace, AssetsFileInstance fileInst)
    {
        var ttTypes = fileInst.file.Metadata.TypeTreeTypes;
        foreach (var type in ttTypes)
        {
            var ttTypeInfo = new TypeTreeTypeInfo(workspace.Manager, fileInst, type);
            TypeTreeTypeInfos.Add(ttTypeInfo);
        }
    }

    private TypeTreeInfo()
    {
    }

    private void SelectedTypeChanged(TypeTreeTypeInfo? typeInfo)
    {
        TypeTreeNodes.Clear();
        if (typeInfo != null)
        {
            SelectedTypeName = typeInfo.Name;
            SelectedTypeId = $"{typeInfo.TypeId} (0x{typeInfo.TypeId:x})";
            SelectedScriptId = typeInfo.ScriptName != string.Empty
                ? $"{typeInfo.ScriptId} ({typeInfo.ScriptName})"
                : typeInfo.ScriptId.ToString();

            SelectedTypeHash = typeInfo.TypeHash.ToString();
            SelectedMonoHash = typeInfo.MonoHash.ToString();
            AddTypeTreeNodes(typeInfo);
        }
        else
        {
            SelectedTypeName = string.Empty;
            SelectedTypeId = string.Empty;
            SelectedScriptId = string.Empty;
            SelectedTypeHash = string.Empty;
            SelectedMonoHash = string.Empty;
            SelectedAligned = string.Empty;
            SelectedTypeFlags = string.Empty;
            SelectedMetaFlags = string.Empty;
        }
    }

    private void SelectedNodeChanged(TypeTreeUINode? uiNode)
    {
        if (uiNode != null)
        {
            var node = uiNode.Node;
            SelectedAligned = (node.MetaFlags & 0x4000) != 0 ? "true" : "false";
            SelectedTypeFlags = node.TypeFlags.ToString();
            SelectedMetaFlags = node.MetaFlags.ToString("X4");
        }
        else
        {
            SelectedAligned = string.Empty;
            SelectedTypeFlags = string.Empty;
            SelectedMetaFlags = string.Empty;
        }
    }
    private static InlineCollection GenerateInlines(TypeTreeType type, TypeTreeNode node)
    {
        var inlines = new InlineCollection();

        var typeName = node.GetTypeString(type.StringBufferBytes);
        var fieldName = node.GetNameString(type.StringBufferBytes);
        var isValueType = AssetTypeValueField.GetValueTypeByTypeName(typeName) != AssetValueType.None;

        var span1 = new Span()
        {
            Foreground = isValueType
                ? PrimNameBrush
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

    private void AddTypeTreeNodes(TypeTreeTypeInfo? typeInfo)
    {
        if (typeInfo is null)
        {
            return;
        }

        var typeTreeType = typeInfo.TtType;
        var flatList = typeTreeType.Nodes;
        if (flatList == null)
        {
            return;
        }

        var lookup = new Dictionary<int, TypeTreeUINode>();
        var rootNodes = new List<TypeTreeUINode>();

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

        TypeTreeNodes.AddRange(rootNodes);
    }

    public static TypeTreeInfo Empty { get; } = new()
    {
        SelectedTypeName = string.Empty,
        SelectedTypeId = string.Empty,
        SelectedScriptId = string.Empty,
        SelectedTypeHash = string.Empty,
        SelectedMonoHash = string.Empty,
        SelectedAligned = string.Empty,

        SelectedTypeFlags = string.Empty,
        SelectedMetaFlags = string.Empty,
    };
}
