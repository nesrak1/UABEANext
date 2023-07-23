using Avalonia.Controls;
using Avalonia.ReactiveUI;
using AvaloniaEdit.Utils;
using ReactiveUI;
using System.Threading.Tasks;
using UABEANext3.AssetWorkspace;
using UABEANext3.Services;
using UABEANext3.ViewModels.Dialogs;
using UABEANext3.ViewModels.Documents;
using UABEANext3.Views.Dialogs;

namespace UABEANext3.Views.Documents
{
    public partial class AssetDocumentView : ReactiveUserControl<AssetDocumentViewModel>
    {
        public AssetDocumentView()
        {
            InitializeComponent();

            this.WhenActivated(action =>
                action(ViewModel!.ShowEditData.RegisterHandler(DoShowDialogAsync)));
        }

        private void DataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is AssetDocumentViewModel docVm)
            {
                var selectedItem = dataGrid.SelectedItem;
                if (selectedItem is AssetInst asset)
                {
                    docVm.InvokeAssetOpened(asset);
                }
            }
        }

        private async Task DoShowDialogAsync(InteractionContext<EditDataViewModel, byte[]?> interaction)
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

                var result = await dialogService.ShowDialog<byte[]?>(dialog);//await dialog.ShowDialog<byte[]?>();
                interaction.SetOutput(result);
            }
        }
    }
}
