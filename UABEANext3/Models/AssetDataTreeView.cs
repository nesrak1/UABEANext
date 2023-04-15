using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UABEANext2;
using UABEANext2.Util;
using UABEANext3.AssetWorkspace;
using UABEANext3.Views;

namespace UABEANext3.Models
{
    public class AssetDataTreeView : TreeView, IStyleable
    {
        Type IStyleable.StyleKey => typeof(TreeView);

        //private Workspace _workspace;

        private AvaloniaList<object> ListItems => (AvaloniaList<object>)Items;

        private static SolidColorBrush PrimNameBrushDark = SolidColorBrush.Parse("#569cd6");
        private static SolidColorBrush PrimNameBrushLight = SolidColorBrush.Parse("#0000ff");
        private static SolidColorBrush TypeNameBrushDark = SolidColorBrush.Parse("#4ec9b0");
        private static SolidColorBrush TypeNameBrushLight = SolidColorBrush.Parse("#2b91af");
        private static SolidColorBrush StringBrushDark = SolidColorBrush.Parse("#d69d85");
        private static SolidColorBrush StringBrushLight = SolidColorBrush.Parse("#a31515");
        private static SolidColorBrush ValueBrushDark = SolidColorBrush.Parse("#b5cea8");
        private static SolidColorBrush ValueBrushLight = SolidColorBrush.Parse("#5b2da8");

        private MenuItem menuEditAsset;
        private MenuItem menuVisitAsset;
        private MenuItem menuExpandSel;
        private MenuItem menuCollapseSel;

        private SolidColorBrush PrimNameBrush
        {
            get
            {
                return true
                    ? PrimNameBrushDark
                    : PrimNameBrushLight;
            }
        }
        private SolidColorBrush TypeNameBrush
        {
            get
            {
                return true
                    ? TypeNameBrushDark
                    : TypeNameBrushLight;
            }
        }
        private SolidColorBrush StringBrush
        {
            get
            {
                return true
                    ? StringBrushDark
                    : StringBrushLight;
            }
        }
        private SolidColorBrush ValueBrush
        {
            get
            {
                return true
                    ? ValueBrushDark
                    : ValueBrushLight;
            }
        }

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
            ContextMenu.Items = new AvaloniaList<MenuItem>()
            {
                menuEditAsset,
                menuVisitAsset,
                menuExpandSel,
                menuCollapseSel
            };

            //ControlTool?.Control = this;
        }

        private async void MenuEditAsset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)SelectedItem;
                if (item.Tag != null)
                {
                    AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;

                    AssetInst? cont = _workspace.GetAssetInst(info.fromFile, 0, info.fromPathId, false);
                    if (cont == null || cont.BaseValueField == null)
                    {
                        return;
                    }

                    //await _win.ShowEditAssetWindow(cont);
                    //await MessageBoxUtil.ShowDialog(null, "Note", "Asset updated. Changes will be shown next time you open this asset.");
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
            if (SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)SelectedItem;
                if (item != null && item.Tag != null)
                {
                    AssetDataTreeViewItem info = (AssetDataTreeViewItem)item.Tag;
                    //_win.SelectAsset(info.fromFile, info.fromPathId);//todo
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
            this._workspace = workspace;
            Reset();
        }

        public void Reset()
        {
            Items = new AvaloniaList<object>();
        }

        public void LoadComponent(AssetInst asset)
        {
            if (_workspace == null)
                return;

            AssetTypeValueField? baseField = _workspace.GetBaseField(asset);

            if (baseField == null)
            {
                TreeViewItem errorItem0 = CreateTreeItem("Asset failed to deserialize.");
                TreeViewItem errorItem1 = CreateTreeItem("The file version may be too new for");
                TreeViewItem errorItem2 = CreateTreeItem("this tpk or the file format is custom.");
                errorItem0.Items = new List<TreeViewItem>() { errorItem1, errorItem2 };
                ListItems.Add(errorItem0);
                return;
            }

            string baseItemString = $"{baseField.TypeName} {baseField.FieldName}";
            if (asset.ClassId == (uint)AssetClassID.MonoBehaviour || asset.ClassId < 0)
            {
                string monoName = Utils.GetMonoBehaviourNameFast(_workspace, asset);
                if (monoName != null)
                {
                    baseItemString += $" ({monoName})";
                }
            }

            TreeViewItem baseItem = CreateTreeItem(baseItemString);

            TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
            baseItem.Items = new AvaloniaList<TreeViewItem>() { arrayIndexTreeItem };
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

                foreach (TreeViewItem treeItemChild in treeItem.Items)
                {
                    ExpandAllChildren(treeItemChild);
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
                foreach (TreeViewItem treeItemChild in treeItem.Items)
                {
                    CollapseAllChildren(treeItemChild);
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
                Foreground = TypeNameBrush,/*,
                FontWeight = FontWeight.Bold*/
            };
            Bold bold1 = new Bold();
            bold1.Inlines.Add(typeName);
            span1.Inlines.Add(bold1);
            tb.Inlines.Add(span1);

            Bold bold2 = new Bold();
            bold2.Inlines.Add($" {fieldName}");
            tb.Inlines.Add(bold2);

            /*
			<Span Foreground="#4ec9b0">
				<Bold>TypeName</Bold></Span>
			<Bold>. fieldName = .</Bold>
			<Span Foreground="#d69d85">
				<Bold>"hi"</Bold>
			</Span> 
            */

            return new TreeViewItem()
            {
                Header = tb
            };
        }

        private TreeViewItem CreateColorTreeItem(string typeName, string fieldName, string middle, string value)
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
            tb.Inlines.Add(span1);

            Bold bold2 = new Bold();
            bold2.Inlines.Add($" {fieldName}");
            tb.Inlines.Add(bold2);

            tb.Inlines.Add(middle);

            if (value != "")
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

            return new TreeViewItem() { Header = tb };
        }

        //lazy load tree items. avalonia is really slow to load if
        //we just throw everything in the treeview at once
        private void SetTreeItemEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetTypeValueField field)
        {
            item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
            //avalonia's treeviews have no Expanded event so this is all we can do
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(new SimpleObserver<bool>(isExpanded =>
            {
                AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again
                    TreeLoad(fromFile, field, fromPathId, item);
                }
            }));
        }

        private void SetPPtrEvents(TreeViewItem item, AssetsFileInstance fromFile, long fromPathId, AssetInst asset)
        {
            item.Tag = new AssetDataTreeViewItem(fromFile, fromPathId);
            var expandObs = item.GetObservable(TreeViewItem.IsExpandedProperty);
            expandObs.Subscribe(new SimpleObserver<bool>(isExpanded =>
            {
                AssetDataTreeViewItem itemInfo = (AssetDataTreeViewItem)item.Tag;
                if (isExpanded && !itemInfo.loaded)
                {
                    itemInfo.loaded = true; //don't load this again

                    if (asset != null)
                    {
                        AssetTypeValueField baseField = _workspace.GetBaseField(asset);
                        TreeViewItem baseItem = CreateTreeItem($"{baseField.TypeName} {baseField.FieldName}");

                        TreeViewItem arrayIndexTreeItem = CreateTreeItem("Loading...");
                        baseItem.Items = new AvaloniaList<TreeViewItem>() { arrayIndexTreeItem };
                        item.Items = new AvaloniaList<TreeViewItem>() { baseItem };
                        SetTreeItemEvents(baseItem, asset.FileInstance, fromPathId, baseField);
                    }
                    else
                    {
                        item.Items = new AvaloniaList<TreeViewItem>() { CreateTreeItem("[null asset]") };
                    }
                }
            }));
        }

        private void TreeLoad(AssetsFileInstance fromFile, AssetTypeValueField assetField, long fromPathId, TreeViewItem treeItem)
        {
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
                if (childField.Value != null)
                {
                    AssetValueType evt = childField.Value.ValueType;
                    string quote = "";
                    if (evt == AssetValueType.String) quote = "\"";
                    if (1 <= (int)evt && (int)evt <= 12)
                    {
                        middle = " = ";
                        value = $"{quote}{childField.AsString}{quote}";
                    }
                    if (evt == AssetValueType.Array)
                    {
                        middle = $" (size {childField.Children.Count})";
                    }
                    else if (evt == AssetValueType.ByteArray)
                    {
                        middle = $" (size {childField.AsByteArray.Length})";
                    }
                }

                if (isArray)
                {
                    TreeViewItem arrayIndexTreeItem = CreateTreeItem($"{arrayIdx}");
                    items.Add(arrayIndexTreeItem);

                    TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value);
                    arrayIndexTreeItem.Items = new AvaloniaList<TreeViewItem>() { childTreeItem };

                    if (childField.Children.Count > 0)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new AvaloniaList<TreeViewItem>() { dummyItem };
                        SetTreeItemEvents(childTreeItem, fromFile, fromPathId, childField);
                    }

                    arrayIdx++;
                }
                else
                {
                    TreeViewItem childTreeItem = CreateColorTreeItem(childField.TypeName, childField.FieldName, middle, value);
                    items.Add(childTreeItem);

                    if (childField.Children.Count > 0)
                    {
                        TreeViewItem dummyItem = CreateTreeItem("Loading...");
                        childTreeItem.Items = new AvaloniaList<TreeViewItem>() { dummyItem };
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

                    AssetInst cont = _workspace.GetAssetInst(fromFile, fileId, pathId, true);

                    TreeViewItem childTreeItem = CreateTreeItem("[view asset]");
                    items.Add(childTreeItem);

                    TreeViewItem dummyItem = CreateTreeItem("Loading...");
                    childTreeItem.Items = new AvaloniaList<TreeViewItem>() { dummyItem };
                    SetPPtrEvents(childTreeItem, fromFile, pathId, cont);
                }
            }

            treeItem.Items = items;
        }

        private AvaloniaList<AssetInst>? _activeAssets = null;
        private Workspace? _workspace = null;

        public static readonly DirectProperty<AssetDataTreeView, AvaloniaList<AssetInst>?> ActiveAssetsProperty =
            AvaloniaProperty.RegisterDirect<AssetDataTreeView, AvaloniaList<AssetInst>?>(nameof(ActiveAssets), o => o.ActiveAssets, (o, v) => o.ActiveAssets = v);
        public static readonly DirectProperty<AssetDataTreeView, Workspace?> WorkspaceProperty =
            AvaloniaProperty.RegisterDirect<AssetDataTreeView, Workspace?>(nameof(Workspace), o => o.Workspace, (o, v) => o.Workspace = v);

        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public AvaloniaList<AssetInst>? ActiveAssets
        {
            get => _activeAssets;
            set
            {
                SetAndRaise(ActiveAssetsProperty, ref _activeAssets, value);
                if (value != null)
                {
                    Reset();
                    foreach (var item in value)
                    {
                        LoadComponent(item);
                    }
                }
            }
        }

        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public Workspace? Workspace
        {
            get => _workspace;
            set => SetAndRaise(WorkspaceProperty, ref _workspace, value);
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
}
