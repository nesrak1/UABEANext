using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
using UABEANext4.AssetWorkspace;
using UABEANext4.Util;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;
public partial class WorkspaceExplorerToolView : UserControl
{
    public WorkspaceExplorerToolView()
    {
        InitializeComponent();

        var selectionChanged = DebounceUtils.Debounce<object>(SolutionTreeView_OnSelectionChanged, 300);
        SolutionTreeView.SelectionChanged += (sender, e) => selectionChanged(e);
        SolutionTreeView.Tapped += SolutionTreeView_Tapped;
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
            var wsItems = new List<WorkspaceItem>();
            foreach (var item in SolutionTreeView.SelectedItems)
            {
                if (item is WorkspaceItem wsItem)
                {
                    wsItems.Add(wsItem);
                }
            }
            viewModel.SelectedItemsChanged(wsItems);
        }
    }

    private void SolutionTreeView_Tapped(object? sender, TappedEventArgs e)
    {
        if (SolutionTreeView.SelectedItem == null)
            return;

        var treeViewItem = (TreeViewItem?)SolutionTreeView.TreeContainerFromItem(SolutionTreeView.SelectedItem);
        if (treeViewItem != null)
        {
            treeViewItem.IsExpanded = true;
        }
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
