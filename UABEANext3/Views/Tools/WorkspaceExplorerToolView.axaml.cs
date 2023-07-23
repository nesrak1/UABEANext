using Avalonia.Controls;
using UABEANext3.AssetWorkspace;
using UABEANext3.ViewModels.Tools;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using UABEANext3.Util;
using Avalonia.Threading;
using System.Diagnostics;

namespace UABEANext3.Views.Tools
{
    public partial class WorkspaceExplorerToolView : UserControl
    {
        public WorkspaceExplorerToolView()
        {
            InitializeComponent();
        }

        int last = 0;
        object lastObj = new();

        private async void SolutionTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var current = Interlocked.Increment(ref last);
            await Task.Delay(200);

            lock (lastObj)
            {
                if (current != last)
                    return;
            }

            if (DataContext is WorkspaceExplorerToolViewModel wsExpVm)
            {
                var selectionActuallyChanged = e.AddedItems.Count > 0 || e.RemovedItems.Count > 0;
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
                    wsExpVm.InvokeSelectedWorkspaceItemChanged(wsItems);
                }
            }
        }
    }
}
