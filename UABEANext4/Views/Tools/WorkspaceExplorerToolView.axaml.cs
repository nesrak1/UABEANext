using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Generic;
using System.Linq;
using UABEANext4.AssetWorkspace;
using UABEANext4.Logic;
using UABEANext4.Util;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;

public partial class WorkspaceExplorerToolView : UserControl
{
    // todo: move contextmenu logic to the view model
    // so we're consistent with asset document view model?
    private readonly ContextMenu _addRemoveUnloadCm;
    private readonly ContextMenu _unloadCm;
    private readonly ContextMenu _renameCm;
    private readonly ContextMenu _defaultCm;

    public WorkspaceExplorerToolView()
    {
        InitializeComponent();

        // right click menu
        _addRemoveUnloadCm = (ContextMenu)Resources["AddRemoveUnloadCm"]!;
        _unloadCm = (ContextMenu)Resources["UnloadCm"]!;
        _renameCm = (ContextMenu)Resources["RenameCm"]!;
        _defaultCm = (ContextMenu)Resources["DefaultCm"]!;

        // events
        var selectionChanged = DebounceUtils.Debounce<object>(SolutionTreeView_OnSelectionChanged, 300);
        SolutionTreeView.SelectionChanged += (sender, e) => selectionChanged(e);
        SolutionTreeView.Tapped += SolutionTreeView_Tapped;
        SolutionTreeView.ContextRequested += SolutionTreeView_ContextRequested;

        // cancel selection change event if we're closing a file
        WeakReferenceMessenger.Default.Register<RequestCloseFileMessage>(this, (e, h) => selectionChanged(null));
    }

    private void SolutionTreeView_OnSelectionChanged(object e)
    {
        if (DataContext == null || DataContext is not WorkspaceExplorerToolViewModel viewModel)
            return;

        if (e is not SelectionChangedEventArgs selectionEventArgs)
            return;

        var selectionActuallyChanged = selectionEventArgs.AddedItems.Count > 0 || selectionEventArgs.RemovedItems.Count > 0;
        if (selectionActuallyChanged)
        {
            var wsItems = GetSelectedWsItems(SolutionTreeView.SelectedItems);
            viewModel.SelectedItemsChanged(wsItems);
        }
    }

    private void SolutionTreeView_Tapped(object? sender, TappedEventArgs e)
    {
        if (SolutionTreeView.SelectedItem == null)
            return;

        var treeViewItem = (TreeViewItem?)SolutionTreeView.TreeContainerFromItem(SolutionTreeView.SelectedItem);
        treeViewItem?.IsExpanded = true;
    }

    private void SolutionTreeView_ContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        ContextMenu contextMenu;

        var wsItems = GetSelectedWsItems(SolutionTreeView.SelectedItems);
        if (wsItems.Count == 0)
        {
            contextMenu = _defaultCm;
        }
        else if (wsItems.Count == 1)
        {
            var wsItem = wsItems[0];
            if (wsItem.ObjectType == WorkspaceItemType.BundleFile)
            {
                contextMenu = _addRemoveUnloadCm;
            }
            else if (
                wsItem.ObjectType == WorkspaceItemType.AssetsFile ||
                wsItem.ObjectType == WorkspaceItemType.ResourceFile)
            {
                if (wsItem.Parent is not null)
                {
                    contextMenu = _renameCm;
                }
                else
                {
                    contextMenu = _unloadCm;
                }
            }
            else
            {
                contextMenu = _defaultCm;
            }
        }
        else
        {
            bool anyUnloadable = wsItems.Any(i =>
                i.ObjectType == WorkspaceItemType.BundleFile ||
                (i.Parent is null &&
                    (i.ObjectType == WorkspaceItemType.AssetsFile
                    || i.ObjectType == WorkspaceItemType.ResourceFile)
                ));

            if (anyUnloadable)
            {
                contextMenu = _unloadCm;
            }
            else
            {
                contextMenu = _defaultCm;
            }
        }

        SolutionTreeView.ContextMenu = contextMenu;
    }

    private static List<WorkspaceItem> GetSelectedWsItems(System.Collections.IList selectedItems)
    {
        var wsItems = new List<WorkspaceItem>();
        foreach (var item in selectedItems)
        {
            if (item is WorkspaceItem wsItem)
            {
                wsItems.Add(wsItem);
            }
        }

        return wsItems;
    }

    private void ExpandAll(object? sender, RoutedEventArgs e)
    {
        foreach (var treeViewObj in SolutionTreeView.Items)
        {
            if (treeViewObj is null)
                continue;

            var treeViewItem = (TreeViewItem?)SolutionTreeView.TreeContainerFromItem(treeViewObj);
            if (treeViewItem is null)
                continue;

            SolutionTreeView.ExpandSubTree(treeViewItem);
        }
    }

    private void CollapseAll(object? sender, RoutedEventArgs e)
    {
        foreach (var treeViewObj in SolutionTreeView.Items)
        {
            if (treeViewObj is null)
                continue;

            var treeViewItem = (TreeViewItem?)SolutionTreeView.TreeContainerFromItem(treeViewObj);
            if (treeViewItem is null)
                continue;

            SolutionTreeView.CollapseSubTree(treeViewItem);
        }
    }
}
