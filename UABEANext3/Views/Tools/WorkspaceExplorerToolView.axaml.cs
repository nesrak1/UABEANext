using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AvaloniaEdit.Utils;
using ReactiveUI;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;
using UABEANext3.Services;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.ViewModels.Documents;
using UABEANext3.ViewModels.Tools;
using UABEANext3.Views.Dialogs;

namespace UABEANext3.Views.Tools
{
    public partial class WorkspaceExplorerToolView : ReactiveUserControl<WorkspaceExplorerToolViewModel>
    {
        public WorkspaceExplorerToolView()
        {
            InitializeComponent();

            this.WhenActivated(action => action(ViewModel!.ShowRenameFile.RegisterHandler(DoShowRenameFileAsync)));

            SolutionTreeView.KeyDown += SolutionTreeView_KeyDown;
        }

        int last = 0;
        object lastObj = new();

        private async void SolutionTreeView_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // SelectedItems signals once for every item
            // wait until all items are added (jank)
            var current = Interlocked.Increment(ref last);
            await Task.Delay(200);

            lock (lastObj)
            {
                if (current != last)
                    return;
            }

            if (ViewModel == null)
                return;

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
                ViewModel.InvokeSelectedWorkspaceItemChanged(wsItems);
            }
        }

        private void SolutionTreeView_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (ViewModel == null)
                return;

            if (e.Key == Avalonia.Input.Key.F2)
            {
                if (SolutionTreeView.SelectedItems.Count > 0)
                {
                    var item = SolutionTreeView.SelectedItems[^1];
                    if (item is WorkspaceItem wsItem)
                    {
                        ViewModel.InvokeSelectedWorkspaceItemRename(wsItem);
                    }
                }
            }
        }

        private async Task DoShowRenameFileAsync(InteractionContext<RenameFileViewModel, string?> interaction)
        {
            if (ViewModel == null)
                return;

            var dialogService = ViewModel.Container.GetService<IDialogService>();
            if (dialogService == null)
            {
                interaction.SetOutput(null);
                return;
            }

            var dialog = new RenameFileView();
            dialog.DataContext = interaction.Input;

            var result = await dialogService.ShowDialog<string?>(dialog);
            interaction.SetOutput(result);
        }
    }
}
