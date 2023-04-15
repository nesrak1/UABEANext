using AssetsTools.NET.Extra;
using Avalonia.Controls;
using UABEANext3.AssetWorkspace;
using UABEANext3.ViewModels.Tools;
using System.Linq;

namespace UABEANext3.Views.Tools
{
    public partial class WorkspaceExplorerToolView : UserControl
    {
        public WorkspaceExplorerToolView()
        {
            InitializeComponent();
        }

        private void SolutionTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is WorkspaceExplorerToolViewModel wsExpVm)
            {
                var selectedItem = e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
                if (selectedItem is WorkspaceItem wsItem)
                {
                    wsExpVm.InvokeSelectedWorkspaceItemChanged(wsItem);
                }
            }
        }
    }
}
