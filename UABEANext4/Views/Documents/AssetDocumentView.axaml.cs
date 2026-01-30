using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.ViewModels.Documents;

namespace UABEANext4.Views.Documents;
public partial class AssetDocumentView : UserControl
{
    public AssetDocumentView()
    {
        InitializeComponent();

        Initialized += AssetDocumentView_Initialized;
        dataGrid.Loaded += DataGrid_Loaded;
    }

    private void AssetDocumentView_Initialized(object? sender, EventArgs e)
    {
        if (DataContext is AssetDocumentViewModel docVm)
        {
            docVm.ShowPluginsContextMenuAction += ShowPluginsContextMenu;
            docVm.SetSelectedItemsAction += SetSelectedItems;
        }
    }

    // doing this from codebehind because it allows the UI to be
    // the source of truth rather than the VM. this comes with the
    // side effect that this won't save if the dock rebuilds the
    // UI, so this may need to be binded to the VM later on...
    private void DataGrid_Loaded(object? sender, RoutedEventArgs e)
    {
        var contextMenu = new ContextMenu();
        for (int i = 0; i < dataGrid.Columns.Count; i++)
        {
            var column = dataGrid.Columns[i];

            // skip columns with no header
            var columnHeader = column.Header.ToString()?.Trim();
            if (string.IsNullOrEmpty(columnHeader))
                continue;

            var contextMenuMenuItem = new MenuItem
            {
                Header = columnHeader,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = column.IsVisible,
                Tag = column
            };

            contextMenuMenuItem.Click += MenuItem_Click;
            contextMenu.Items.Add(contextMenuMenuItem);
        }

        if (dataGrid.FindDescendantOfType<DataGridColumnHeadersPresenter>() is { } columnHeadersPresenter)
        {
            columnHeadersPresenter.ContextMenu = contextMenu;
        }
    }

    private void ShowPluginsContextMenu()
    {
        FlyoutBase.ShowAttachedFlyout(showPluginsBtn);
    }

    // necessary since SelectedItems isn't bindable
    private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is AssetDocumentViewModel docVm)
        {
            var selectedAssetInsts = dataGrid.SelectedItems.Cast<AssetInst>().ToList();
            docVm.OnAssetOpened(selectedAssetInsts);
        }
    }

    private void MenuItem_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;

        if (menuItem.Tag is not DataGridColumn dgColumn)
            return;

        dgColumn.IsVisible = menuItem.IsChecked;
    }

    // probably not great for many items but fine for a few
    private void SetSelectedItems(List<AssetInst> assets)
    {
        if (DataContext is AssetDocumentViewModel docVm)
        {
            dataGrid.SelectedItems.Clear();
            foreach (var asset in assets)
            {
                if (docVm.Items.Contains(asset))
                    dataGrid.SelectedItems.Add(asset);
            }

            if (assets.Count > 0)
            {
                dataGrid.ScrollIntoView(assets[0], null);
            }
        }
    }

    private void MenuFlyout_Opening(object? sender, EventArgs e)
    {
        if (DataContext is AssetDocumentViewModel vm)
        {
            vm.CreateContextMenu();
        }
    }
}
