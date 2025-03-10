using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Util;
using static UABEANext4.Themes.TypeHighlightingBrushes;

namespace UABEANext4.Controls;
public class AssetDataTreeView : TreeView
{
    protected override Type StyleKeyOverride => typeof(TreeView);

    //private Workspace _workspace;

    private AvaloniaList<object> ListItems = new AvaloniaList<object>();

    private MenuItem menuEditAsset;
    private MenuItem menuVisitAsset;
    private MenuItem menuExpandSel;
    private MenuItem menuCollapseSel;

    public AssetDataTreeView() : base()
    {
        menuEditAsset = new MenuItem() { Header = "Edit Asset" };
        menuVisitAsset = new MenuItem() { Header = "Visit Asset" };
        menuExpandSel = new MenuItem() { Header = "Expand Selection" };
        menuCollapseSel = new MenuItem() { Header = "Collapse Selection" };

        DoubleTapped += AssetDataTreeView_DoubleTapped;
        menuEditAsset.Click += MenuEditAsset_Click;
        menuVisitAsset.Click += MenuVisitAsset_Click;
        menuExpandSel.Click += MenuExpandSel_Click;
        menuCollapseSel.Click += MenuCollapseSel_Click;

        ContextMenu = new ContextMenu();
        ContextMenu.ItemsSource = new AvaloniaList<MenuItem>()
        {
            menuEditAsset,
            menuVisitAsset,
            menuExpandSel,
            menuCollapseSel
        };

        ActiveAssetsProperty.Changed.Subscribe(e =>
        {
            var value = e.NewValue.Value;
            if (value != null)
            {
                value.CollectionChanged += (s, e) => LoadAssets(value);
                LoadAssets(value);
            }
        });
    }

    private void MenuEditAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedItem != null && _workspace != null)
        {
            TreeViewItem item = (TreeViewItem)SelectedItem;
            if (item.Tag != null)
            {
                AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;

                AssetInst? cont = _workspace.GetAssetInst(info.fromFile, 0, info.fromPathId);
                if (cont == null)
                {
                    return;
                }

                RequestEditAsset(cont);
            }
        }
    }

    private void AssetDataTreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (SelectedItem != null)
        {
            TreeViewItem item = (TreeViewItem)SelectedItem;
            item.IsExpanded = !item.IsExpanded;
        }
    }

    private void MenuVisitAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedItem != null && _workspace != null)
        {
            TreeViewItem item = (TreeViewItem)SelectedItem;
            if (item != null && item.Tag != null)
            {
                AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;
                AssetInst? cont = _workspace.GetAssetInst(info.fromFile, 0, info.fromPathId);
                if (cont == null)
                {
                    return;
                }

                RequestVisitAsset(cont);
            }
        }
    }

    private void MenuExpandSel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedItem != null)
        {
            ExpandAllChildren((TreeViewItem)SelectedItem);
        }
    }

    private void MenuCollapseSel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedItem != null)
        {
            CollapseAllChildren((TreeViewItem)SelectedItem);
        }
    }

    public bool HasInitialized()
    {
        return _workspace != null;
    }

    public void Init(Workspace workspace)
    {
        _workspace = workspace;
        Reset();
    }

    public void Reset()
    {
        ListItems = new AvaloniaList<object>();
        ItemsSource = ListItems;
    }

    public void LoadComponent(AssetInst asset)
    {
        if (_workspace == null)
            return;

        AssetTypeValueField? baseField = _workspace.GetBaseField(asset);
        if (baseField == null)
        {
            TreeViewItem errorItem0 = CreateTreeItem("Asset failed to deserialize. A few possibilities:");
            TreeViewItem errorItem1 = CreateTreeItem("The game's version is too new for this version of UABEA");
            TreeViewItem errorItem1I = CreateTreeItem("Try updating UABEA to see if it fixes the problem.");
            TreeViewItem errorItem2 = CreateTreeItem("The asset was a MonoBehaviour that didn't read correctly");
            TreeViewItem errorItem2I = CreateTreeItem("Try disabling Cpp2IL and dumping dlls manually into a (new) folder called Managed.");
            TreeViewItem errorItem3 = CreateTreeItem("The game uses a custom engine");
            TreeViewItem errorItem3I = CreateTreeItem("I can't help with custom/encrypted engines. You're on your own.");
            errorItem0.ItemsSource = new List<TreeViewItem>() { errorItem1, errorItem2, errorItem3 };
            errorItem1.ItemsSource = new List<TreeViewItem>() { errorItem1I };
            errorItem2.ItemsSource = new List<TreeViewItem>() { errorItem2I };
            errorItem3.ItemsSource = new List<TreeViewItem>() { errorItem3I };
            ListItems.Add(errorItem0);
            return;
        }

        string baseItemString = $"{baseField.TypeName} {baseField.FieldName}";
        if (asset.Type == AssetClassID.MonoBehaviour || asset.TypeId < 0)
        {
            string monoName;
            lock (asset.FileInstance.LockReader)
            {
                monoName = AssetNameUtils.GetMonoBehaviourNameFast(_workspace, asset);
            }
            if (monoName != null)
            {
                baseItemString += $" ({monoName})";
            }
        }

        TreeViewItem baseItem = CreateTreeItem(baseItemString);

        TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
        baseItem.ItemsSource = new AvaloniaList<TreeViewItem>() { arrayIndexTreeItem };
        ListItems.Add(baseItem);

        SetTreeItemEvents(baseItem, asset.FileInstance, asset.PathId, baseField);
        baseItem.IsExpanded = true;
    }

    public void ExpandAllChildren(TreeViewItem treeItem)
    {
        string? text = null;
        if (treeItem.Header is string header)
        {
            text = header;
        }
        else if (treeItem.Header is TextBlock rtb)
        {
            text = rtb.Text;
        }

        if (text != "[view asset]")
        {
            treeItem.IsExpanded = true;

            var treeItems = treeItem.Items.Cast<TreeViewItem?>();
            foreach (TreeViewItem? treeItemChild in treeItems)
            {
                if (treeItemChild != null)
                {
                    ExpandAllChildren(treeItemChild);
                }
            }
        }
    }

    public void CollapseAllChildren(TreeViewItem treeItem)
    {
        string? text = null;
        if (treeItem.Header is string header)
        {
            text = header;
        }
        else if (treeItem.Header is TextBlock rtb)
        {
            text = rtb.Text;
        }

        if (text != "[view asset]")
        {
            var treeItems = treeItem.Items.Cast<TreeViewItem?>();
            foreach (TreeViewItem? treeItemChild in treeItems)
            {
                if (treeItemChild != null)
                {
                    CollapseAllChildren(treeItemChild);
                }
            }

            treeItem.IsExpanded = false;
        }
    }

    private TreeViewItem CreateTreeItem(string text)
    {
        return new TreeViewItem() { Header = text };
    }

    private TreeViewItem CreateColorTreeItem(string typeName, string fieldName)
    {
        TextBlock tb = new TextBlock();

        Span span1 = new Span()
        {
            Foreground = TypeNameBrush
        };
        Bold bold1 = new Bold();
        bold1.Inlines.Add(typeName);
        span1.Inlines.Add(bold1);
        tb.Inlines!.Add(span1);

        Bold bold2 = new Bold();
        bold2.Inlines.Add($" {fieldName}");
        tb.Inlines.Add(bold2);

        return new TreeViewItem()
        {
            Header = tb
        };
    }

    private TreeViewItem CreateColorTreeItem(string typeName, string fieldName, string middle, string value, string comment = "")
    {
        bool isString = value.StartsWith("\"");

        TextBlock tb = new TextBlock();

        bool primitiveType = AssetTypeValueField.GetValueTypeByTypeName(typeName) != AssetValueType.None;

        Span span1 = new Span()
        {
            Foreground = primitiveType ? PrimNameBrush : TypeNameBrush
        };
        Bold bold1 = new Bold();
        bold1.Inlines.Add(typeName);
        span1.Inlines.Add(bold1);
        tb.Inlines!.Add(span1);

        Bold bold2 = new Bold();
        bold2.Inlines.Add($" {fieldName}");
        tb.Inlines.Add(bold2);

        tb.Inlines.Add(middle);

        if (value != string.Empty)
        {
            Span span2 = new Span()
            {
                Foreground = isString ? StringBrush : ValueBrush
            };
            Bold bold3 = new Bold();
            bold3.Inlines.Add(value);
            span2.Inlines.Add(bold3);
            tb.Inlines.Add(span2);
        }

        if (comment != string.Empty)
        {
            tb.Inlines.Add(comment);
        }

        return new TreeViewItem() { Header = tb };
    }

    // lazy load tree items. avalonia is really slow to load if
    // we just throw everything in the treeview at once
    private void SetTreeItemEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetTypeValueField field)
    {
        item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
        // avalonia's treeviews have no Expanded event so this is all we can do
        var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
        expandObs.Subscribe(new SimpleObserver<bool>(isExpanded =>
        {
            AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
            if (isExpanded && !itemInfo.loaded)
            {
                itemInfo.loaded = true; // don't load this again
                TreeLoad(fromFile, field, fromPathId, item);
            }
        }));
    }

    private void SetPPtrEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetInst? asset)
    {
        item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
        var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
        expandObs.Subscribe(new SimpleObserver<bool>(isExpanded =>
        {
            AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
            if (isExpanded && !itemInfo.loaded)
            {
                itemInfo.loaded = true; // don't load this again

                if (asset == null)
                {
                    item.ItemsSource = new AvaloniaList<TreeViewItem>() { CreateTreeItem("[null asset]") };
                    return;
                }

                AssetTypeValueField? baseField = _workspace!.GetBaseField(asset);
                if (baseField == null)
                {
                    item.ItemsSource = new AvaloniaList<TreeViewItem>() { CreateTreeItem("[failed to load]") };
                    return;
                }

                TreeViewItem baseItem = CreateTreeItem($"{baseField.TypeName} {baseField.FieldName}");
                TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
                baseItem.ItemsSource = new AvaloniaList<TreeViewItem>() { arrayIndexTreeItem };
                item.ItemsSource = new AvaloniaList<TreeViewItem>() { baseItem };
                SetTreeItemEvents(baseItem, asset.FileInstance, fromPathId, baseField);
            }
        }));
    }

    private void TreeLoad(AssetsFileInstance fromFile, AssetTypeValueField assetField, long fromPathId, TreeViewItem treeItem)
    {
        List<AssetTypeValueField> children;
        if (assetField.Value != null && assetField.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
            children = assetField.AsManagedReferencesRegistry.references.Select(r => r.data).ToList();
        else
            children = assetField.Children;

        if (assetField.Children.Count == 0)
            return;

        int arrayIdx = 0;
        AvaloniaList<TreeViewItem> items = new AvaloniaList<TreeViewItem>(assetField.Children.Count + 1);

        AssetTypeTemplateField assetFieldTemplate = assetField.TemplateField;
        bool isArray = assetFieldTemplate.IsArray;

        if (isArray)
        {
            int size = assetField.AsArray.size;
            AssetTypeTemplateField sizeTemplate = assetFieldTemplate.Children[0];
            TreeViewItem arrayIndexTreeItem = CreateColorTreeItem(sizeTemplate.Type, sizeTemplate.Name, " = ", size.ToString());
            items.Add(arrayIndexTreeItem);
        }

        foreach (AssetTypeValueField childField in assetField)
        {
            if (childField == null) return;
            string middle = "";
            string value = "";
            string comment = "";
            if (childField.Value != null)
            {
                AssetValueType valueType = childField.Value.ValueType;
                if (valueType == AssetValueType.String)
                {
                    middle = " = ";
                    value = EscapeAndQuoteString(childField.AsString);
                }
                else if (1 <= (int)valueType && (int)valueType <= 11)
                {
                    middle = " = ";
                    value = childField.AsString;
                }
                if (valueType == AssetValueType.Array)
                {
                    middle = $" (size {childField.Children.Count})";
                }
                else if (valueType == AssetValueType.ByteArray)
                {
                    byte[] bytes = childField.AsByteArray;
                    int byteArraySize = childField.AsByteArray.Length;
                    middle = $" (size {byteArraySize}) = ";

                    const int MAX_PREVIEW_BYTES = 20;
                    int previewSize = Math.Min(byteArraySize, MAX_PREVIEW_BYTES);

                    StringBuilder valueBuilder = new StringBuilder();
                    for (int i = 0; i < previewSize; i++)
                    {
                        if (i == 0)
                        {
                            valueBuilder.Append(bytes[i].ToString("X2"));
                        }
                        else
                        {
                            valueBuilder.Append(" " + bytes[i].ToString("X2"));
                        }
                    }

                    if (byteArraySize > MAX_PREVIEW_BYTES)
                    {
                        valueBuilder.Append(" ...");
                    }

                    value = valueBuilder.ToString();
                }

                if (valueType == AssetValueType.Int32 && childField.TemplateField.Name == "m_FileID")
                {
                    List<AssetsFileExternal> externals = fromFile.file.Metadata.Externals;
                    int fileId = childField.AsInt;
                    if (fileId == 0)
                    {
                        comment = $" ({fromFile.name})";
                    }
                    else
                    {
                        int externalIdx = fileId - 1;
                        if (0 <= externalIdx && externalIdx < externals.Count)
                        {
                            AssetsFileExternal external = externals[externalIdx];
                            string pathName = external.PathName;
                            string externalName;
                            if (pathName == string.Empty && external.Type != AssetsFileExternalType.Normal)
                            {
                                externalName = $"guid: {external.Guid}";
                            }
                            else
                            {
                                externalName = Path.GetFileName(externals[externalIdx].PathName);
                            }
                            comment = $" ({externalName})";
                        }
                    }
                }
            }

            bool hasChildren = childField.Children.Count > 0;

            if (isArray)
            {
                TreeViewItem arrayIndexTreeItem = CreateTreeItem($"{arrayIdx}");
                items.Add(arrayIndexTreeItem);

                TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value);
                arrayIndexTreeItem.ItemsSource = new AvaloniaList<TreeViewItem>() { childTreeItem };

                if (hasChildren)
                {
                    TreeViewItem dummyItem = CreateTreeItem("Loading...");
                    childTreeItem.ItemsSource = new AvaloniaList<TreeViewItem>() { dummyItem };
                    SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                }

                arrayIdx++;
            }
            else
            {
                TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value, comment);
                items.Add(childTreeItem);

                if (childField.Value != null && childField.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
                {
                    TreeLoadManagedRegistry(childTreeItem, childField, fromFile, fromPathId);
                }

                if (hasChildren)
                {
                    TreeViewItem dummyItem = CreateTreeItem("Loading...");
                    childTreeItem.ItemsSource = new AvaloniaList<TreeViewItem> { dummyItem };
                    SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                }
            }
        }

        string templateFieldType = assetField.TypeName;
        if (templateFieldType.StartsWith("PPtr<") && templateFieldType.EndsWith(">"))
        {
            var fileIdField = assetField["m_FileID"];
            var pathIdField = assetField["m_PathID"];
            bool pptrValid = !fileIdField.IsDummy && !pathIdField.IsDummy;

            if (pptrValid)
            {
                int fileId = fileIdField.AsInt;
                long pathId = pathIdField.AsLong;

                AssetInst? cont = _workspace!.GetAssetInst(fromFile, fileId, pathId);

                TreeViewItem childTreeItem = CreateTreeItem("[view asset]");
                items.Add(childTreeItem);

                TreeViewItem dummyItem = CreateTreeItem("Loading...");
                childTreeItem.ItemsSource = new AvaloniaList<TreeViewItem>() { dummyItem };
                SetPPtrEvents(childTreeItem, fromFile, pathId, cont);
            }
        }

        treeItem.ItemsSource = items;
    }

    private void TreeLoadManagedRegistry(TreeViewItem childTreeItem, AssetTypeValueField childField, AssetsFileInstance fromFile, long fromPathId)
    {
        ManagedReferencesRegistry registry = childField.AsManagedReferencesRegistry;

        if (registry.version == 1 || registry.version == 2)
        {
            TreeViewItem versionItem = CreateColorTreeItem("int", "version", " = ", registry.version.ToString());
            TreeViewItem refIdsItem = CreateColorTreeItem("vector", "RefIds");
            TreeViewItem refIdsArrayItem = CreateColorTreeItem("Array", "Array", $" (size {registry.references.Count})", "");

            AvaloniaList<TreeViewItem> refObjItems = new AvaloniaList<TreeViewItem>();

            foreach (AssetTypeReferencedObject refObj in registry.references)
            {
                AssetTypeReference typeRef = refObj.type;

                TreeViewItem refObjItem = CreateColorTreeItem("ReferencedObject", "data");

                TreeViewItem managedTypeItem = CreateColorTreeItem("ReferencedManagedType", "type");
                managedTypeItem.ItemsSource = new AvaloniaList<TreeViewItem>
                {
                    CreateColorTreeItem("string", "class", " = ", EscapeAndQuoteString(typeRef.ClassName)),
                    CreateColorTreeItem("string", "ns", " = ", EscapeAndQuoteString(typeRef.Namespace)),
                    CreateColorTreeItem("string", "asm", " = ", EscapeAndQuoteString(typeRef.AsmName))
                };

                TreeViewItem refObjectItem = CreateColorTreeItem("ReferencedObjectData", "data");

                TreeViewItem dummyItem = CreateTreeItem("Loading...");
                refObjectItem.ItemsSource = new AvaloniaList<TreeViewItem> { dummyItem };
                SetTreeItemEvents(refObjectItem, fromFile, fromPathId, refObj.data);

                if (registry.version == 1)
                {
                    refObjItem.ItemsSource = new AvaloniaList<TreeViewItem>
                    {
                        managedTypeItem,
                        refObjectItem
                    };
                }
                else if (registry.version == 2)
                {
                    refObjItem.ItemsSource = new AvaloniaList<TreeViewItem>
                    {
                        CreateColorTreeItem("SInt64", "rid", " = ", refObj.rid.ToString()),
                        managedTypeItem,
                        refObjectItem
                    };
                }

                refObjItems.Add(refObjItem);
            }

            refIdsArrayItem.ItemsSource = refObjItems;

            refIdsItem.ItemsSource = new AvaloniaList<TreeViewItem>
            {
                refIdsArrayItem
            };

            childTreeItem.ItemsSource = new AvaloniaList<TreeViewItem>
            {
                versionItem,
                refIdsItem
            };
        }
        else
        {
            TreeViewItem errorTreeItem = CreateTreeItem($"[unsupported registry version {registry.version}]");
            childTreeItem.ItemsSource = new AvaloniaList<TreeViewItem> { errorTreeItem };
        }
    }

    // escaping format tbd
    private string EscapeAndQuoteString(string str)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("\"");
        if (str.Length > 1000)
        {
            sb.Append(str[..1000]);
            sb.Append("...");
        }
        else
        {
            sb.Append(str);
        }
        sb.Append("\"");
        return sb.ToString();
    }

    private static void RequestEditAsset(AssetInst asset)
    {
        if (asset != null)
        {
            WeakReferenceMessenger.Default.Send(new RequestEditAssetMessage(asset));
        }
    }

    private static void RequestVisitAsset(AssetInst asset)
    {
        if (asset != null)
        {
            WeakReferenceMessenger.Default.Send(new RequestVisitAssetMessage(asset));
        }
    }

    private Workspace? _workspace = null;
    private ObservableCollection<AssetInst>? _activeAssets = null;

    public static readonly DirectProperty<AssetDataTreeView, Workspace?> WorkspaceProperty =
        AvaloniaProperty.RegisterDirect<AssetDataTreeView, Workspace?>(nameof(Workspace), o => o.Workspace, (o, v) => o.Workspace = v);
    public static readonly DirectProperty<AssetDataTreeView, ObservableCollection<AssetInst>?> ActiveAssetsProperty =
        AvaloniaProperty.RegisterDirect<AssetDataTreeView, ObservableCollection<AssetInst>?>(nameof(ActiveAssets), o => o.ActiveAssets, (o, v) => o.ActiveAssets = v);

    public Workspace? Workspace
    {
        get => _workspace;
        set => SetAndRaise(WorkspaceProperty, ref _workspace, value);
    }

    public ObservableCollection<AssetInst>? ActiveAssets
    {
        get => _activeAssets;
        set => SetAndRaise(ActiveAssetsProperty, ref _activeAssets, value);
    }

    private void LoadAssets(ObservableCollection<AssetInst> activeAssets)
    {
        Reset();
        foreach (var item in activeAssets)
        {
            LoadComponent(item);
        }
    }
}

public class AssetDataTreeViewItem
{
    public bool loaded;
    public AssetsFileInstance fromFile;
    public long fromPathId;

    public AssetDataTreeViewItem(AssetsFileInstance fromFile, long fromPathId)
    {
        this.loaded = false;
        this.fromFile = fromFile;
        this.fromPathId = fromPathId;
    }
}
