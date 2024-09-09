using Avalonia.Controls;
using UABEANext4.Logic.Hierarchy;
using UABEANext4.ViewModels.Tools;

namespace UABEANext4.Views.Tools;
public partial class HierarchyToolView : UserControl
{
    public HierarchyToolView()
    {
        InitializeComponent();
    }

    private void HierarchyTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is HierarchyToolViewModel hrVm)
        {
            var selectedItem = e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
            if (selectedItem is HierarchyItem explorerItem && explorerItem.Asset != null)
            {
                hrVm.SelectedItemsChanged(explorerItem.Asset);
            }
        }
    }
}
