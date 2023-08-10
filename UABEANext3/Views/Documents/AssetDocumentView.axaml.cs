using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AvaloniaEdit.Utils;
using ReactiveUI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;
using UABEANext3.Services;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.ViewModels.Documents;
using UABEANext3.Views.Dialogs;
using static UABEANext3.ViewModels.Dialogs.BatchImportViewModel;

namespace UABEANext3.Views.Documents
{
    public partial class AssetDocumentView : ReactiveUserControl<AssetDocumentViewModel>
    {
        public AssetDocumentView()
        {
            InitializeComponent();

            this.WhenActivated(action => action(ViewModel!.ShowEditData.RegisterHandler(DoShowEditDataAsync)));
            this.WhenActivated(action => action(ViewModel!.ShowBatchImport.RegisterHandler(DoShowBatchImportAsync)));
            this.WhenActivated(action => action(ViewModel!.ShowSelectDump.RegisterHandler(DoShowSelectDumpAsync)));
        }

        // necessary since SelectedItems isn't bindable
        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is AssetDocumentViewModel docVm)
            {
                var selectedAssetInsts = dataGrid.SelectedItems.Cast<AssetInst>().ToList();
                docVm.InvokeAssetOpened(selectedAssetInsts);
            }
        }

        private async Task DoShowEditDataAsync(InteractionContext<EditDataViewModel, byte[]?> interaction)
        {
            if (DataContext is AssetDocumentViewModel docVm)
            {
                var dialogService = docVm.Container.GetService<IDialogService>();
                if (dialogService == null)
                {
                    interaction.SetOutput(null);
                    return;
                }

                var dialog = new EditDataView();
                dialog.DataContext = interaction.Input;

                var result = await dialogService.ShowDialog<byte[]?>(dialog);
                interaction.SetOutput(result);
            }
        }

        private async Task DoShowBatchImportAsync(InteractionContext<BatchImportViewModel, List<ImportBatchInfo>> interaction)
        {
            if (DataContext is AssetDocumentViewModel docVm)
            {
                var dialogService = docVm.Container.GetService<IDialogService>();
                if (dialogService == null)
                {
                    interaction.SetOutput(new List<ImportBatchInfo>(0));
                    return;
                }

                var dialog = new BatchImportView();
                dialog.DataContext = interaction.Input;

                var result = await dialogService.ShowDialog<List<ImportBatchInfo>>(dialog);
                interaction.SetOutput(result);
            }
        }

        private async Task DoShowSelectDumpAsync(InteractionContext<SelectDumpViewModel, SelectedDumpType?> interaction)
        {
            if (ViewModel == null)
                return;

            var dialogService = ViewModel.Container.GetService<IDialogService>();
            if (dialogService == null)
            {
                interaction.SetOutput(null);
                return;
            }

            var dialog = new SelectDumpView();
            dialog.DataContext = interaction.Input;

            var result = await dialogService.ShowDialog<SelectedDumpType?>(dialog);
            interaction.SetOutput(result);
        }
    }
}
