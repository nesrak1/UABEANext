using System.Linq;
using Avalonia.Controls;
using UABEANext4.ViewModels.Dialogs;

namespace UABEANext4.Views.Dialogs
{
    public partial class AssetInfoView : Window
    {
        public AssetInfoView()
        {
            InitializeComponent();
        }

        // private void AssetInfoView_Loaded(
        //     object? sender,
        //     Avalonia.Interactivity.RoutedEventArgs e
        // ) 
        // {  
        //     if (DataContext is AssetInfoViewModel aivm)
        //     {
        //         this.SelectedAssetComboBox.ItemsSource = aivm.Items;
        //         this.SelectedAssetComboBox.SelectedIndex = 0;
        //         aivm.SelectedItem = aivm.Items.FirstOrDefault();
        //     }
        // }
    }
}
