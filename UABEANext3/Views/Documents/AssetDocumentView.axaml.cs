using Avalonia.Controls;
using Avalonia.Input;
using UABEANext3.AssetWorkspace;
using UABEANext3.ViewModels.Documents;
using UABEANext3.ViewModels.Tools;

namespace UABEANext3.Views.Documents
{
    public partial class AssetDocumentView : UserControl
    {
        public AssetDocumentView()
        {
            InitializeComponent();
        }

        private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
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
    }
}
